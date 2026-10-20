# EEG no VR SNC (integração com o Protobrain)

Atualizado em 19/10/2026. Explica como a gravação de EEG processada pelo Protobrain chega ao VR, o que cada parte mostra e o que ela não mostra. Os scripts estão listados em `MAPA_DOS_SCRIPTS_VR_SNC.md`.

## O que vem de onde

O Protobrain (`Documentos/protobrain-main`) continua sendo um projeto separado. O pipeline dele (Python/MNE) faz todo o processamento e grava `data/build/`. O Unity só lê esse resultado; nada de Python roda no VR.

| Etapa | Onde | O quê |
|---|---|---|
| Processamento | Protobrain, `python python.py build` | Leitura do BDF (BioSemi, 32 canais de escalpo), filtros, canais ruins, ICA + ICLabel, potência por banda (STFT, janela de 2 s, passo de 0,25 s), spline esférica, atlas Desikan e Brodmann no fsaverage |
| Importação | Unity, **Ferramentas > VR SNC > EEG > Importar dados do Protobrain** | Copia os binários para `Assets/EEG/Dados/*.bytes`, converte o escalpo para o Unity e grava `Assets/EEG/DadosEEG.asset` |
| Instalação | **Ferramentas > VR SNC > EEG > Instalar EEG na cena** | Alinha o fsaverage ao cérebro do modelo, liga cada eletrodo a um giro, cria `Controlador_EEG` e a aba EEG; salva a cena |

A gravação atual é `sub-hc1_ses-hc_task-rest_eeg.bdf`: 192 s a 512 Hz, 32 canais, rede de 60 Hz, F3 interpolado. O `.bdf` não entra no projeto Unity, só os resultados (cerca de 16 MB). Confirme a licença e a procedência do conjunto de dados antes de publicar o aplicativo, como o README do Protobrain já recomenda.

## Eixos e unidades

O Protobrain usa o padrão do three.js: x = direita, y = superior, z = posterior, com 1 unidade = 100 mm. No Unity, a importação converte para (x, y, −z) × 0,1 m, com z = anterior. O espelhamento do eixo z inverte o sentido das faces; o importador confere as normais do escalpo e corrige o sentido se for preciso.

## Alinhamento ao cérebro do modelo

O cérebro do VR (BodyParts3D) e o fsaverage vêm de pessoas diferentes, então o alinhamento é aproximado. Ele é feito no espaço local de `Sistema_Nervoso_Central`:

- **Eixos do modelo:** direita = centro dos giros direitos − esquerdos; anterior = lobo frontal − occipital; superior = córtex − tronco encefálico.
- **Escala:** por eixo, pela extensão dos dois córtices, limitada a ±15% da escala média para não deformar o escalpo.
- **Centro:** o centro do córtex do fsaverage vai para o centro do córtex do modelo.

Resultado na cena atual:
- o lobo do ponto do córtex do modelo mais próximo de cada eletrodo coincide com o lobo do atlas Desikan em 30 dos 32 eletrodos;
- distância eletrodo → córtex do modelo: mediana de 22 mm, de 18 a 26 mm (no fsaverage, o Protobrain mede de 17 a 30 mm).

A cabeça EEG fica fora da hierarquia anatômica (o verificador externo continua PASS) e segue a pose da raiz do SNC a cada quadro. Por isso ela acompanha o modelo quando o usuário o move ou gira, mas não acompanha uma peça explodida.

## Eletrodo → giro

O Protobrain dá, para cada eletrodo, a região Desikan-Killiany e a área de Brodmann do ponto do córtex mais próximo no fsaverage. A tabela de `CorrespondenciaGirosEEG` leva a região Desikan à estrutura equivalente do modelo, pelo nome em português e pelo lado:

- frontal médio (rostral) → giro frontal médio;
- partes triangular, opercular e orbital → giro frontal inferior;
- pré-central, pós-central, supramarginal, parietal superior, temporal médio e occipital lateral → giros de mesmo nome;
- parietal inferior → giro angular (equivalência aproximada).

Os 32 eletrodos têm estrutura no modelo; 4 deles (P3, PO3, P4 e PO4, todos no parietal inferior) usam a equivalência aproximada, e a aba mostra "(aprox.)".

Os dois atlas podem divergir (no polo occipital o giro é o occipital lateral, mas a área é a 17). Os dois aparecem na aba, cada um com o seu nome.

## O que o VR mostra

- **Escalpo:** a potência medida em cada eletrodo, interpolada pela spline esférica (λ = 0) e presa entre o menor e o maior valor medido no quadro, como no Protobrain. Fora da área interpolada a cor se mistura com "sem dado" (#1a1f28). O mapa troca a cada 0,25 s (4 quadros por segundo); o de voltagem acompanha o cursor.
- **Rampas de cor:** as do Protobrain, interpoladas em OKLab. Divergente (azul, cinza neutro, vermelho) para a variação no tempo e a voltagem; sequencial (azul escuro a claro) para potência absoluta e relativa. Nunca arco-íris.
- **Mapa 2D (topoplot):** numa janela própria, aberta pelo botão **Mapa 2D** da aba. Usa a projeção do EEGLAB (cabeça de raio 0,5, nariz para cima, esquerda do sujeito à esquerda), com 6 isolinhas, os nomes dos 32 eletrodos (botão **Nomes**), a barra de cores com limites e unidade e o eletrodo escolhido em vermelho. A janela tem também tocar/pausar, as 5 bandas e a frase "Espaço de sensores: não é a fonte do sinal nem diagnóstico". Tocar no mapa escolhe o eletrodo mais próximo do ponto tocado, como tocar no escalpo.
- **Traçados:** negativo para cima; sensibilidade única de 7 µV/mm; Butterworth de fase zero com −3 dB exatamente no corte escolhido, de 0,5 a 70 Hz; notch da rede opcional; referência média em ordem de leitura ou dupla banana (ACNS). Os filtros são os do navegador do Protobrain, portados para C#. A suíte confere em sinais sintéticos: o DC sai, 10 Hz passa sem perda, o notch tira 60 Hz e o corte de 70 Hz fica em −3 dB.

## O que não mostra

- **O mapa é espaço de sensores.** A cor é a potência medida no escalpo. O cérebro por dentro nunca recebe a cor do mapa.
- **O giro sob o eletrodo é correspondência anatômica num cérebro-modelo, não a fonte do sinal.** Por condução de volume, cada eletrodo capta uma área ampla do córtex. Localizar fontes exige outro método (BEM, lead field, eLORETA).
- **A variação no tempo (z) não é normativa:** compara cada canal com a própria média na gravação.
- **Não é diagnóstico** nem substitui a leitura do traçado por médico com formação em EEG (Nuwer, 1997, AAN/ACNS).
- **Anatomia de template:** posições 10-05 no fsaverage, não as do sujeito; e o modelo do VR é outra pessoa.

## Desempenho

Medido no PC (Editor, Mono), para orientar o teste no Quest:

| Operação | Tempo | Frequência |
|---|---|---|
| Pintura do escalpo (10.242 vértices × 32 canais) | 1,5 ms | 4 Hz na potência; no máximo 15 Hz na voltagem |
| Mapa 2D (256 × 256 px) | 3,7 ms para calcular, fora da thread principal; menos de 0,1 ms para enviar à textura | 4 Hz na potência; no máximo 10 Hz na voltagem |
| Filtragem completa (32 canais × 192 s, com notch) | 145 ms | Só ao trocar filtros, fora da thread principal |

No Quest esses tempos devem ser algumas vezes maiores; vale medir o FPS com o escalpo translúcido e as duas janelas abertas.

## Testes

- `VerificacoesEEG.Validar`, na suíte de preparação do commit, confere:
  - os dados importados e o carregamento;
  - a interpolação (dentro da faixa medida; pesos somando 1);
  - os filtros e as rampas;
  - as montagens (32 e 18 faixas; Fp1−F7 é a diferença dos canais);
  - as 32 ligações aos giros, com Fp1 no giro frontal médio;
  - a reserva de mão (a mão que aponta um eletrodo não dá foco nem pinça à anatomia; a outra mão não é afetada);
  - o toque no mapa 2D (tocar na posição de cada um dos 32 eletrodos escolhe o próprio eletrodo; fora da cabeça, nada).
- Em Play Mode, com o simulador, foram conferidos:
  - a aba e o escalpo;
  - a posição anatômica dos eletrodos (Fp1 sobre o frontal, Oz sobre o occipital, Cz no alto, T7 e T8 nos lados certos);
  - o raio acertando o eletrodo e o gatilho selecionando o giro sem sair da aba;
  - com mãos simuladas: o raio da mão apontando o eletrodo, a pinça selecionando o giro sem agarrar a anatomia atrás, e o raio e o laser do controle calados enquanto a mão está ativa;
  - a janela de traçados (montagens, filtros em segundo plano, página e cursor sincronizados);
  - a janela do mapa 2D (mapa desenhado, título com banda e modo, os 32 nomes, tocar sobre O2 escolhe O2 e o giro dele, alça e virada de frente ao soltar).
