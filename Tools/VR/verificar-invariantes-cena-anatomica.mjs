#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const raizProjeto = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
// Referência versionada capturada no marco anterior às correções de 10/10/2026.
// Não depende de backups ignorados pelo Git ou de caminhos pessoais.
const baselinePadrao = path.join(raizProjeto, 'Tools', 'VR', 'Referencias', 'CenaAnatomica.unity');
const atualPadrao = path.join(raizProjeto, 'Assets', 'Scenes', 'SampleScene.unity');
const caminhoOracle = path.join(raizProjeto, 'Assets', 'Resources', 'AnatomicalOracle.json');
const idObjetoRaiz = '6000000000000000001';
const idTransformRaiz = '6000000000000000002';
const shaBaselineEsperado = 'D68112F84D158EA3F72E395D4D8BCBFE51279227ED64E7D3851405A51609D2B0';
const limiteDiferencas = 200;

const caminhosScripts = {
  binding: path.join(raizProjeto, 'Assets', 'Scripts', 'Anatomia', 'VinculoAnatomico.cs.meta'),
  node: path.join(raizProjeto, 'Assets', 'Scripts', 'Interacao', 'NoInteracaoAnatomica.cs.meta'),
  hand: path.join(raizProjeto, 'Assets', 'Scripts', 'Interacao', 'ElementoInteragivelMao.cs.meta'),
};

// O Unity grava os nomes atuais dos campos ao salvar a cena, enquanto a referência
// versionada ainda usa os nomes históricos. Os dois lados são reescritos para os nomes
// declarados hoje nos scripts, conforme seus atributos [FormerlySerializedAs].
const classesScripts = {
  binding: { arquivo: path.join(raizProjeto, 'Assets', 'Scripts', 'Anatomia', 'VinculoAnatomico.cs'), classe: 'VinculoAnatomico' },
  node: { arquivo: path.join(raizProjeto, 'Assets', 'Scripts', 'Interacao', 'NoInteracaoAnatomica.cs'), classe: 'NoInteracaoAnatomica' },
  hand: { arquivo: path.join(raizProjeto, 'Assets', 'Scripts', 'Interacao', 'ElementoInteragivelMao.cs'), classe: 'ElementoInteragivelMao' },
};

// Atualizado -> baseline. Whitelist fechada: outros valores serializados continuam estritos.
const editorClassIdentifierCanonico = Object.freeze({
  'Assembly-CSharp::VRSNC.Anatomia.VinculoAnatomico': 'Assembly-CSharp::VRSNC.Anatomy.BrainAnatomyBinding',
  'Assembly-CSharp::VRSNC.Interacao.NoInteracaoAnatomica': 'Assembly-CSharp::VRSNC.Interaction.BrainInteractionNode',
  'Assembly-CSharp::VRSNC.Interacao.ElementoInteragivelMao': 'Assembly-CSharp::VRSNC.Interaction.BrainHandInteractable',
});

function calcularSha256(caminhoArquivo) {
  return crypto.createHash('sha256').update(fs.readFileSync(caminhoArquivo)).digest('hex').toUpperCase();
}

function normalizarTexto(texto) {
  return texto.replace(/\r\n/g, '\n').replace(/\r/g, '\n');
}

function argumentos() {
  const posicionais = process.argv.slice(2).filter((argumento) => !argumento.startsWith('--'));
  return {
    baseline: path.resolve(posicionais[0] || baselinePadrao),
    current: path.resolve(posicionais[1] || atualPadrao),
  };
}

function extrairGuidMeta(caminhoArquivo, erros) {
  if (!fs.existsSync(caminhoArquivo)) {
    erros.push(`metadado de script ausente: ${path.relative(raizProjeto, caminhoArquivo)}`);
    return null;
  }
  const correspondencia = normalizarTexto(fs.readFileSync(caminhoArquivo, 'utf8')).match(/^guid:\s*([0-9a-f]+)\s*$/mi);
  if (!correspondencia) {
    erros.push(`GUID ausente no metadado de script: ${path.relative(raizProjeto, caminhoArquivo)}`);
    return null;
  }
  return correspondencia[1];
}

function extrairNomesHistoricos(definicao, erros) {
  const mapa = new Map();
  if (!fs.existsSync(definicao.arquivo)) {
    erros.push(`script anatômico ausente: ${path.relative(raizProjeto, definicao.arquivo)}`);
    return mapa;
  }
  const fonte = normalizarTexto(fs.readFileSync(definicao.arquivo, 'utf8'));
  const declaracao = fonte.match(new RegExp(`^[ \\t]*public[^\\n]*\\bclass\\s+${definicao.classe}\\b[^\\n]*$`, 'm'));
  if (!declaracao) {
    erros.push(`classe ${definicao.classe} não encontrada em ${path.relative(raizProjeto, definicao.arquivo)}`);
    return mapa;
  }
  // Somente o corpo do MonoBehaviour: contratos JSON do mesmo arquivo reutilizam nomes históricos.
  const resto = fonte.slice(declaracao.index + declaracao[0].length);
  const fim = resto.search(/\n {4}[^ {}\n]/);
  const corpo = fim >= 0 ? resto.slice(0, fim) : resto;
  let pendentes = [];
  for (const linha of corpo.split('\n')) {
    for (const historico of linha.matchAll(/FormerlySerializedAs\("([^"]+)"\)/g)) pendentes.push(historico[1]);
    if (/^\s*(\[|\/\/|$)/.test(linha)) continue;
    const campo = linha.match(/^\s*(?:private|public|protected|internal)\s+(?!static\b|const\b)[\w.<>,[\]\s?]+?\s+(\w+)\s*(?:=(?!>)[^;]*)?;\s*$/);
    if (campo) for (const historico of pendentes) mapa.set(historico, campo[1]);
    pendentes = [];
  }
  if (mapa.size === 0) erros.push(`nenhum nome histórico de campo encontrado em ${definicao.classe}`);
  return mapa;
}

// Reescreve as chaves de primeiro nível para os nomes atuais e as ordena, de modo que a
// mesma serialização com nomes antigos ou novos produza exatamente o mesmo texto. Linhas de
// continuação (quatro ou mais espaços) são unidas à anterior com um espaço, como no YAML: o Unity
// quebra textos longos em pontos diferentes conforme o comprimento do nome do campo.
function canonizarBlocoAnatomico(bruto, mapa) {
  const linhas = normalizarTexto(bruto).split('\n');
  while (linhas.length > 0 && linhas[linhas.length - 1] === '') linhas.pop();
  const cabecalho = [];
  const campos = [];
  let atual = null;
  for (const linha of linhas) {
    const campo = linha.match(/^ {2}([A-Za-z_]\w*):(.*)$/);
    if (campo) {
      const chave = mapa.get(campo[1]) ?? campo[1];
      atual = { chave, linhas: [`  ${chave}:${campo[2]}`] };
      campos.push(atual);
    } else if (atual && /^ {4,}\S/.test(linha)) {
      atual.linhas[atual.linhas.length - 1] += ` ${linha.trim()}`;
    } else if (atual) {
      atual.linhas.push(linha);
    } else {
      cabecalho.push(linha);
    }
  }
  campos.sort((primeiro, segundo) => (primeiro.chave < segundo.chave ? -1 : primeiro.chave > segundo.chave ? 1 : 0));
  return [...cabecalho, ...campos.flatMap((entrada) => entrada.linhas)].join('\n');
}

function analisarBlocosYaml(caminhoArquivo, erros) {
  if (!fs.existsSync(caminhoArquivo)) {
    erros.push(`cena ausente: ${caminhoArquivo}`);
    return null;
  }
  const fonte = normalizarTexto(fs.readFileSync(caminhoArquivo, 'utf8'));
  const correspondencias = [...fonte.matchAll(/^--- !u!(\d+) &([^\s]+)[^\n]*\n/gm)];
  if (correspondencias.length === 0) {
    erros.push(`nenhum bloco YAML do Unity encontrado: ${caminhoArquivo}`);
    return null;
  }
  const blocos = new Map();
  for (let indice = 0; indice < correspondencias.length; indice += 1) {
    const correspondencia = correspondencias[indice];
    const inicio = correspondencia.index;
    const fim = indice + 1 < correspondencias.length ? correspondencias[indice + 1].index : fonte.length;
    const bruto = fonte.slice(inicio, fim);
    const id = correspondencia[2];
    if (blocos.has(id)) erros.push(`fileID duplicado do Unity: ${id}`);
    blocos.set(id, { id, type: Number(correspondencia[1]), raw: bruto, lines: bruto.split('\n') });
  }
  return blocos;
}

function valorCampo(bloco, campo) {
  const correspondencia = bloco.raw.match(new RegExp(`^\\s*${campo}:\\s*(.*?)\\s*$`, 'm'));
  return correspondencia ? correspondencia[1] : null;
}

function idReferencia(valor) {
  const correspondencia = valor && valor.match(/\{fileID:\s*(-?\d+)/);
  return correspondencia ? correspondencia[1] : null;
}

function todasReferencias(valor) {
  return [...(valor || '').matchAll(/\{fileID:\s*(-?\d+)/g)].map((correspondencia) => correspondencia[1]);
}

function listaCampo(bloco, campo) {
  const linhas = bloco.lines;
  const inicio = linhas.findIndex((linha) => new RegExp(`^\\s*${campo}:\\s*$`).test(linha));
  if (inicio < 0) return [];
  const valores = [];
  for (let indice = inicio + 1; indice < linhas.length; indice += 1) {
    const linha = linhas[indice];
    if (/^\s*$/.test(linha)) continue;
    if (!/^\s*-\s/.test(linha)) break;
    const correspondencia = linha.match(/fileID:\s*(-?\d+)/);
    if (correspondencia && correspondencia[1] !== '0') valores.push(correspondencia[1]);
  }
  return valores;
}

function classeEditor(bloco) {
  return valorCampo(bloco, 'm_EditorClassIdentifier') || '';
}

function classificarComponente(bloco, guidsScripts = {}) {
  const nomeClasse = classeEditor(bloco);
  const guidScript = valorCampo(bloco, 'm_Script')?.match(/guid:\s*([0-9a-f]+)/i)?.[1];
  if (nomeClasse.includes('VinculoAnatomico') || guidScript === guidsScripts.binding) return 'binding';
  if (nomeClasse.includes('NoInteracaoAnatomica') || guidScript === guidsScripts.node) return 'node';
  if (nomeClasse.includes('ElementoInteragivelMao') || guidScript === guidsScripts.hand) return 'hand';
  if (nomeClasse.includes('MeshFilter')) return 'meshFilter';
  if (bloco.type === 33) return 'meshFilter';
  if (nomeClasse.includes('MeshCollider')) return 'meshCollider';
  if (bloco.type === 64) return 'meshCollider';
  if (bloco.type === 114 && /m_Mesh:|m_Colliders:/.test(bloco.raw)) return 'meshOrCollider';
  return null;
}

function ehTransform(bloco) { return bloco?.type === 4; }
function ehObjetoJogo(bloco) { return bloco?.type === 1; }

function serializarTransform(bloco) {
  return {
    parent: idReferencia(valorCampo(bloco, 'm_Father')),
    children: listaCampo(bloco, 'm_Children'),
    localPosition: valorCampo(bloco, 'm_LocalPosition'),
    localRotation: valorCampo(bloco, 'm_LocalRotation'),
    localScale: valorCampo(bloco, 'm_LocalScale'),
  };
}

function serializarObjetoJogo(bloco) {
  return {
    name: valorCampo(bloco, 'm_Name'),
    active: valorCampo(bloco, 'm_IsActive'),
  };
}

function analisarCena(caminhoArquivo, erros, guidsScripts, mapasCampos) {
  const blocos = analisarBlocosYaml(caminhoArquivo, erros);
  if (!blocos) return null;
  const transformRaiz = blocos.get(idTransformRaiz);
  const objetoRaiz = blocos.get(idObjetoRaiz);
  if (!transformRaiz || !objetoRaiz || !ehTransform(transformRaiz) || !ehObjetoJogo(objetoRaiz)) {
    erros.push(`blocos raiz obrigatórios do SNC ausentes ou com tipo incorreto: GO ${idObjetoRaiz}, Transform ${idTransformRaiz}`);
    return null;
  }

  const transforms = new Map();
  const objetosJogo = new Map();
  for (const bloco of blocos.values()) {
    if (ehTransform(bloco)) transforms.set(bloco.id, bloco);
    if (ehObjetoJogo(bloco)) objetosJogo.set(bloco.id, bloco);
  }

  const descendentes = [];
  const visitados = new Set();
  const visitar = (idTransform) => {
    if (visitados.has(idTransform)) {
      erros.push(`ciclo de Transform ou filho repetido: ${idTransform}`);
      return;
    }
    const transform = transforms.get(idTransform);
    if (!transform) {
      erros.push(`bloco Transform descendente ausente: ${idTransform}`);
      return;
    }
    visitados.add(idTransform);
    const idObjetoJogo = idReferencia(valorCampo(transform, 'm_GameObject'));
    if (!idObjetoJogo || !objetosJogo.has(idObjetoJogo)) {
      // Instâncias de prefab serializam blocos Transform stripped sem payload
      // local de GameObject. Elas continuam descendentes válidos da cena, mas
      // seus campos de Transform pertencentes ao prefab não ficam neste YAML.
      return;
    }
    descendentes.push({ transformId: idTransform, gameObjectId: idObjetoJogo, transform, gameObject: objetosJogo.get(idObjetoJogo) });
    for (const idFilho of listaCampo(transform, 'm_Children')) visitar(idFilho);
  };
  visitar(idTransformRaiz);

  const idsObjetosDescendentes = new Set(descendentes.map((entrada) => entrada.gameObjectId));
  const donosComponentes = new Map();
  for (const entrada of descendentes) {
    for (const idComponente of listaCampo(entrada.gameObject, 'm_Component')) donosComponentes.set(idComponente, entrada.gameObjectId);
  }

  const componentesRelevantes = new Map();
  const contagensComponentes = { binding: 0, node: 0, hand: 0, meshFilter: 0, meshCollider: 0 };
  for (const [idComponente, idObjetoJogo] of donosComponentes) {
    const componente = blocos.get(idComponente);
    if (!componente) {
      erros.push(`GameObject ${idObjetoJogo} referencia componente ausente: ${idComponente}`);
      continue;
    }
    const tipo = classificarComponente(componente, guidsScripts);
    if (!tipo) continue;
    if (tipo === 'meshOrCollider') {
      // Não amplie o escopo para componentes de runtime serializados arbitrários.
      if (!/m_Mesh:|m_Colliders:/.test(componente.raw)) continue;
    }
    componentesRelevantes.set(idComponente, { component: componente, gameObjectId: idObjetoJogo, kind: tipo });
    if (Object.prototype.hasOwnProperty.call(contagensComponentes, tipo)) contagensComponentes[tipo] += 1;
  }

  // Folhas de prefab anatômico são GameObjects stripped; por isso seus
  // componentes serializados não são alcançados na travessia local. Recupere
  // apenas os três scripts anatômicos conhecidos por GUID, sem incluir
  // controllers, input, UI ou MonoBehaviours de painéis neste fallback.
  for (const bloco of blocos.values()) {
    if (componentesRelevantes.has(bloco.id)) continue;
    if (bloco.type !== 114) continue;
    const tipo = classificarComponente(bloco, guidsScripts);
    if (!['binding', 'node', 'hand'].includes(tipo)) continue;
    const idObjetoJogo = idReferencia(valorCampo(bloco, 'm_GameObject'));
    if (!idObjetoJogo) continue;
    componentesRelevantes.set(bloco.id, { component: bloco, gameObjectId: idObjetoJogo, kind: tipo });
    contagensComponentes[tipo] += 1;
  }

  for (const entrada of componentesRelevantes.values()) {
    if (mapasCampos[entrada.kind]) entrada.canonical = canonizarBlocoAnatomico(entrada.component.raw, mapasCampos[entrada.kind]);
  }

  const vinculacoes = [...componentesRelevantes.values()].filter((entrada) => entrada.kind === 'binding');
  const nos = [...componentesRelevantes.values()].filter((entrada) => entrada.kind === 'node');
  const interagiveisMao = [...componentesRelevantes.values()].filter((entrada) => entrada.kind === 'hand');
  const chaveGrupo = mapasCampos.binding.get('isGroup') ?? 'isGroup';
  const grupos = vinculacoes.filter((entrada) => new RegExp(`^\\s*${chaveGrupo}:\\s*1\\s*$`, 'm').test(entrada.canonical));
  const estruturas = vinculacoes.filter((entrada) => new RegExp(`^\\s*${chaveGrupo}:\\s*0\\s*$`, 'm').test(entrada.canonical));

  const blocosAnatomia = new Map();
  const adicionarEscopo = (id, tipo, textoCanonico) => {
    const bloco = blocos.get(id);
    if (bloco) blocosAnatomia.set(id, { kind: tipo, raw: textoCanonico ?? bloco.raw });
  };
  for (const entrada of descendentes) {
    adicionarEscopo(entrada.transformId, 'Transform');
    if (entrada.gameObjectId !== idObjetoRaiz) adicionarEscopo(entrada.gameObjectId, 'GameObject');
  }
  for (const entrada of componentesRelevantes.values()) adicionarEscopo(entrada.component.id, entrada.kind, entrada.canonical);

  // Referências de malha e estado de collider são invariantes explícitas.
  // Outros campos de componentes de runtime (controllers, input, painéis etc.)
  // permanecem excluídos.
  const invarianteMalha = new Map();
  for (const entrada of componentesRelevantes.values()) {
    if (entrada.kind !== 'meshFilter' && entrada.kind !== 'meshCollider' && entrada.kind !== 'meshOrCollider') continue;
    invarianteMalha.set(entrada.component.id, {
      owner: entrada.gameObjectId,
      kind: entrada.kind,
      mesh: valorCampo(entrada.component, 'm_Mesh'),
      enabled: valorCampo(entrada.component, 'm_Enabled'),
      convex: valorCampo(entrada.component, 'm_Convex'),
      trigger: valorCampo(entrada.component, 'm_IsTrigger'),
      colliders: listaCampo(entrada.component, 'm_Colliders'),
    });
  }

  const invarianteTransform = new Map();
  const invarianteObjetoJogo = new Map();
  for (const entrada of descendentes) {
    invarianteTransform.set(entrada.transformId, serializarTransform(entrada.transform));
    invarianteObjetoJogo.set(entrada.gameObjectId, serializarObjetoJogo(entrada.gameObject));
  }

  const blocosPrefab = new Map([...blocos.values()]
    .filter((bloco) => bloco.type === 1001)
    .map((bloco) => [bloco.id, bloco]));
  const invariantePrefabAnatomia = new Map();
  for (const entrada of vinculacoes) {
    const objetoJogo = blocos.get(entrada.gameObjectId);
    const idPrefab = idReferencia(valorCampo(objetoJogo, 'm_PrefabInstance'));
    const prefab = idPrefab ? blocosPrefab.get(idPrefab) : null;
    if (!prefab || invariantePrefabAnatomia.has(idPrefab)) continue;
    const modificacao = prefab.raw.match(/\n  m_Modification:[\s\S]*$/)?.[0] || '';
    invariantePrefabAnatomia.set(idPrefab, {
      sourceGuids: [...new Set([...modificacao.matchAll(/guid:\s*([0-9a-f]+)/gi)].map((correspondencia) => correspondencia[1]))].sort(),
      propertyPaths: [...modificacao.matchAll(/^\s*propertyPath:\s*(\S.*)$/gm)].map((correspondencia) => correspondencia[1]),
      modification: modificacao,
    });
  }

  return {
    blocks: blocos,
    descendants: descendentes,
    descendantGameObjectIds: idsObjetosDescendentes,
    relevantComponents: componentesRelevantes,
    anatomyBlocks: blocosAnatomia,
    transformInvariant: invarianteTransform,
    gameObjectInvariant: invarianteObjetoJogo,
    meshInvariant: invarianteMalha,
    anatomyPrefabInvariant: invariantePrefabAnatomia,
    counts: {
      descendants: descendentes.length,
      gameObjects: idsObjetosDescendentes.size,
      bindings: vinculacoes.length,
      groups: grupos.length,
      structures: estruturas.length,
      nodes: nos.length,
      hands: interagiveisMao.length,
      meshFilters: contagensComponentes.meshFilter,
      meshColliders: contagensComponentes.meshCollider,
      anatomyPrefabInstances: invariantePrefabAnatomia.size,
    },
  };
}

function compararMapas(nome, antes, depois, diferencas) {
  const ids = new Set([...antes.keys(), ...depois.keys()]);
  for (const id of [...ids].sort()) {
    const anterior = antes.get(id);
    const posterior = depois.get(id);
    if (!anterior) diferencas.push({ kind: `${nome}:added`, fileID: id });
    else if (!posterior) diferencas.push({ kind: `${nome}:removed`, fileID: id });
    else if (JSON.stringify(anterior) !== JSON.stringify(posterior)) diferencas.push({ kind: `${nome}:changed`, fileID: id });
  }
}

function normalizarEditorClassIdentifierConhecido(bruto, tipo) {
  if (!['binding', 'node', 'hand'].includes(tipo)) return normalizarTexto(bruto);
  return normalizarTexto(bruto).replace(/^([ \t]*m_EditorClassIdentifier:[ \t]*)(.*?)[ \t]*$/m, (linha, prefixo, valor) => {
    const canonico = editorClassIdentifierCanonico[valor.trim()];
    return canonico ? prefixo + canonico : linha;
  });
}

function compararBlocosAnatomia(baseline, atual, diferencas) {
  const ids = new Set([...baseline.anatomyBlocks.keys(), ...atual.anatomyBlocks.keys()]);
  for (const id of [...ids].sort()) {
    const anterior = baseline.anatomyBlocks.get(id);
    const posterior = atual.anatomyBlocks.get(id);
    if (!anterior) diferencas.push({ kind: 'anatomyBlock:added', fileID: id, scope: posterior.kind });
    else if (!posterior) diferencas.push({ kind: 'anatomyBlock:removed', fileID: id, scope: anterior.kind });
    else if (normalizarEditorClassIdentifierConhecido(anterior.raw, anterior.kind) !== normalizarEditorClassIdentifierConhecido(posterior.raw, posterior.kind)) diferencas.push({ kind: 'anatomyBlock:changed', fileID: id, scope: anterior.kind });
  }
}

function compararInvariantesPrefab(baseline, atual, diferencas) {
  compararMapas('AnatomyPrefab', baseline.anatomyPrefabInvariant, atual.anatomyPrefabInvariant, diferencas);
}

function principal() {
  const caminhos = argumentos();
  const erros = [];
  const guidsScripts = {
    binding: extrairGuidMeta(caminhosScripts.binding, erros),
    node: extrairGuidMeta(caminhosScripts.node, erros),
    hand: extrairGuidMeta(caminhosScripts.hand, erros),
  };
  const mapasCampos = {
    binding: extrairNomesHistoricos(classesScripts.binding, erros),
    node: extrairNomesHistoricos(classesScripts.node, erros),
    hand: extrairNomesHistoricos(classesScripts.hand, erros),
  };
  const referencia = analisarCena(caminhos.baseline, erros, guidsScripts, mapasCampos);
  const atual = analisarCena(caminhos.current, erros, guidsScripts, mapasCampos);
  const diferencas = [];

  if (referencia && atual) {
    compararMapas('Transform', referencia.transformInvariant, atual.transformInvariant, diferencas);
    compararMapas('GameObject', referencia.gameObjectInvariant, atual.gameObjectInvariant, diferencas);
    compararMapas('Mesh', referencia.meshInvariant, atual.meshInvariant, diferencas);
    compararInvariantesPrefab(referencia, atual, diferencas);
    compararBlocosAnatomia(referencia, atual, diferencas);
  }

  const shaBaseline = fs.existsSync(caminhos.baseline) ? calcularSha256(caminhos.baseline) : null;
  const shaAtual = fs.existsSync(caminhos.current) ? calcularSha256(caminhos.current) : null;
  const shaOracle = fs.existsSync(caminhoOracle) ? calcularSha256(caminhoOracle) : null;
  const contagensEsperadas = {
    bindings: 272,
    groups: 32,
    structures: 240,
    nodes: 272,
    hands: 240,
    anatomyPrefabInstances: 240,
  };
  const falhasContagem = [];
  for (const [chave, esperado] of Object.entries(contagensEsperadas)) {
    if (referencia && referencia.counts[chave] !== esperado) falhasContagem.push(`baseline ${chave}=${referencia.counts[chave]} esperado ${esperado}`);
    if (atual && atual.counts[chave] !== esperado) falhasContagem.push(`atual ${chave}=${atual.counts[chave]} esperado ${esperado}`);
  }
  erros.push(...falhasContagem);
  if (shaBaseline !== shaBaselineEsperado) erros.push(`SHA256 do baseline divergente: esperado ${shaBaselineEsperado}, obtido ${shaBaseline}`);

  let status = 'PASS';
  if (erros.length > 0 || !referencia || !atual) status = 'BLOCKED';
  else if (diferencas.length > 0) status = 'FAIL';
  const diferencasLimitadas = diferencas.slice(0, limiteDiferencas);
  const resultado = {
    status,
    baseline: { path: path.relative(raizProjeto, caminhos.baseline), sha256: shaBaseline },
    current: { path: path.relative(raizProjeto, caminhos.current), sha256: shaAtual },
    oracle: { path: path.relative(raizProjeto, caminhoOracle), sha256: shaOracle },
    scripts: guidsScripts,
    root: { gameObjectFileID: idObjetoRaiz, transformFileID: idTransformRaiz },
    counts: { baseline: referencia?.counts || null, current: atual?.counts || null },
    scope: {
      compared: [
        'todos os descendentes da raiz Transform de Sistema_Nervoso_Central',
        'nomes e estados ativos dos GameObjects descendentes',
        'pai/filhos/posição local/rotação/escala dos Transforms descendentes',
        'blocos serializados de VinculoAnatomico, NoInteracaoAnatomica e ElementoInteragivelMao, com nomes históricos de campos ([FormerlySerializedAs]) normalizados para os nomes atuais',
        'referências de malha de MeshFilter/MeshCollider e estado explícito de collider',
        '240 blocos de instâncias de prefab das folhas anatômicas: GUIDs-fonte, serialização completa de m_Modification e caminhos de propriedades',
      ],
      excluded: [
        'campos de componentes do controller de runtime da raiz e serialização de input/UI/painéis/controllers',
        'objetos fora da hierarquia raiz do SNC',
        'alegações de conteúdo bibliográfico ou semântico',
      ],
    },
    diffs: diferencasLimitadas,
    diffCount: diferencas.length,
    diffTruncated: diferencas.length > limiteDiferencas,
    errors: erros,
  };
  process.stdout.write(`${JSON.stringify(resultado, null, 2)}\n`);
  process.exitCode = status === 'PASS' ? 0 : 1;
}

principal();
