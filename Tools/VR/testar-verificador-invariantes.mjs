import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const pastaFerramentas = path.dirname(fileURLToPath(import.meta.url));
const verificador = path.join(pastaFerramentas, 'verificar-invariantes-cena-anatomica.mjs');
const referencia = path.join(pastaFerramentas, 'Referencias', 'CenaAnatomica.unity');
const temporario = fs.mkdtempSync(path.join(os.tmpdir(), 'vr-snc-invariantes-'));
function executar(...argumentos) {
  const execucao = spawnSync(process.execPath, [verificador, ...argumentos], { encoding: 'utf8' });
  assert.equal(execucao.error, undefined);
  return { codigo: execucao.status, resultado: JSON.parse(execucao.stdout) };
}
try {
  const atual = executar();
  assert.equal(atual.codigo, 0, JSON.stringify(atual.resultado.errors));
  assert.equal(atual.resultado.status, 'PASS');
  const texto = fs.readFileSync(referencia, 'utf8');
  // Ao salvar a cena, o Unity grava os nomes atuais dos campos. Isso não é alteração anatômica.
  const nomesAtuais = {
    isGroup: 'ehGrupo', catalogId: 'catalogoId', conceptId: 'conceitoId', sourceScientificName: 'nomeCientificoFonte',
    displayScientificName: 'nomeCientificoExibicao', memberStructures: 'membroEstruturas', nodeName: 'nomeNo',
    displayName: 'nomeExibicao', nodeLevel: 'nivelNo', parentNode: 'paiNo', childNodes: 'nosFilhos',
    isSelectable: 'ehSelecionavel', isExplodable: 'ehExpansivel', isCurrentFocus: 'ehAtualFoco', isRootNode: 'ehRaizNo',
    originalParent: 'paiOriginal', originalLocalPosition: 'posicaoLocalOriginal', originalLocalRotation: 'rotacaoLocalOriginal',
    originalLocalScale: 'escalaLocalOriginal', originalActiveState: 'estadoAtivoOriginal', structureInfo: 'estruturaInformacao',
    anatomyBinding: 'vinculoAnatomico', node: 'no', bridge: 'ponte',
  };
  const salvaComNomesAtuais = path.join(temporario, 'CenaSalvaComNomesAtuais.unity');
  const renomeado = texto.replace(/^( {2})(\w+):/gm, (linha, recuo, chave) => (nomesAtuais[chave] ? `${recuo}${nomesAtuais[chave]}:` : linha));
  // O Unity também quebra textos longos em linhas de continuação; o valor YAML é o mesmo.
  const quebrado = renomeado.replace(/^( {2}nomeExibicao: ".{50,}?) (\S+")$/gm, '$1\n    $2');
  assert.notEqual(quebrado, renomeado);
  fs.writeFileSync(salvaComNomesAtuais, quebrado);
  const salva = executar(referencia, salvaComNomesAtuais);
  assert.equal(salva.codigo, 0, JSON.stringify(salva.resultado.errors));
  assert.equal(salva.resultado.status, 'PASS');
  assert.equal(salva.resultado.counts.current.groups, 32);
  // A normalização dos nomes não pode esconder uma alteração real de valor.
  const salvaAlterada = path.join(temporario, 'CenaSalvaComNomesAtuaisAlterada.unity');
  fs.writeFileSync(salvaAlterada, fs.readFileSync(salvaComNomesAtuais, 'utf8').replace(/^( {2}catalogoId: ).*$/m, '$1ID_ADULTERADO'));
  const adulteradaSalva = executar(referencia, salvaAlterada);
  assert.equal(adulteradaSalva.codigo, 1);
  assert.equal(adulteradaSalva.resultado.status, 'FAIL');
  const alterada = path.join(temporario, 'CenaAlterada.unity');
  const raiz = texto.indexOf('--- !u!4 &6000000000000000002');
  assert.ok(raiz >= 0);
  const inicioPosicao = texto.indexOf('  m_LocalPosition:', raiz);
  assert.ok(inicioPosicao > raiz);
  const fimPosicao = texto.indexOf('\n', inicioPosicao);
  fs.writeFileSync(alterada, texto.slice(0, inicioPosicao) + '  m_LocalPosition: {x: 123, y: 456, z: 789}' + texto.slice(fimPosicao));
  const mutacao = executar(referencia, alterada);
  assert.equal(mutacao.codigo, 1);
  assert.equal(mutacao.resultado.status, 'FAIL');
  assert.ok(mutacao.resultado.diffCount > 0);
  const ausente = executar(path.join(temporario, 'Ausente.unity'));
  assert.equal(ausente.codigo, 1);
  assert.equal(ausente.resultado.status, 'BLOCKED');
  const adulterada = executar(alterada, referencia);
  assert.equal(adulterada.codigo, 1);
  assert.ok(adulterada.resultado.errors.some(erro => erro.includes('SHA256')));
  console.log('APROVADO: cena atual íntegra; cena salva com nomes atuais aceita; alteração anatômica detectada; referência ausente recusada; referência adulterada recusada.');
} finally {
  // Diretório criado exclusivamente por este teste, fora do projeto.
  fs.rmSync(temporario, { recursive: true, force: true });
}
