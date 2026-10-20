using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRSNC.Interacao;

namespace VRSNC.FerramentasEditor
{
    /// <summary>Regressão exclusiva do modo de execução para o contrato temporário de transformação global explodida.</summary>
    internal static class VerificacoesRegressaoGestos
    {
        private const float Epsilon = 0.00001f;
        [MenuItem("Ferramentas/VR SNC/Mãos/Validar Expansão Global (Modo de Jogo)", false, 220)]
        private static void ValidarExpansaoGlobal()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("O teste de expansão global exige o Modo de Jogo; o estado da cena não foi alterado.");
                return;
            }

            var falhas = new List<string>();
            var raiz = GameObject.Find("Sistema_Nervoso_Central");
            var global = raiz != null ? raiz.GetComponent<ControladorExpansaoGlobal>() : null;
            var coordenador = raiz != null ? raiz.GetComponent<CoordenadorGestoAnatomico>() : null;
            var primeiro = new GameObject("GlobalExplodeTest_Left")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var segundo = new GameObject("GlobalExplodeTest_Right")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Instantaneo referencia = null;
            try
            {
                Exigir(raiz != null && global != null && coordenador != null, "Raiz global ou coordenador ausente", falhas);
                if (falhas.Count > 0)
                    throw new InvalidOperationException("Os pré-requisitos para a expansão global não foram atendidos.");
                global.CancelarERestaurar();
                Exigir(global.GarantirIndiceAtualizado() && global.QuantidadeFolhasIndexadas == ControladorExpansaoGlobal.QuantidadeFolhasEsperada && global.QuantidadeGruposIndexados == ControladorExpansaoGlobal.QuantidadeGruposEsperada && global.QuantidadeTransformacoesGrupoIndexadas == ControladorExpansaoGlobal.QuantidadeGruposEsperada - 1, "240 elementos terminais, 32 grupos no catálogo incluindo a raiz e 31 nós de grupo que controlam posições descendentes", falhas);
                var folhas = raiz.GetComponentsInChildren<ElementoInteragivelMao>(true);
                Exigir(folhas.Length == ControladorExpansaoGlobal.QuantidadeFolhasEsperada, "Os 240 alvos de colisores exatos foram preservados", falhas);
                if (folhas.Length < 2)
                    throw new InvalidOperationException("A expansão global exige dois alvos em elementos terminais.");
                referencia = Instantaneo.Capturar(raiz, folhas);
                primeiro.transform.position = raiz.transform.position;
                segundo.transform.position = raiz.transform.position + Vector3.right * 0.18f;
                var ajuste = AjustesGesto.Ler(coordenador);
                Exigir(Mathf.Abs(ajuste.ZonaMorta - 0.004f) < Epsilon && Mathf.Abs(ajuste.VariacaoCompleta - 0.18f) < Epsilon, "A calibração serializada do gesto define zona morta de 4 mm e deslocamento total de 18 cm", falhas);
                Exigir(coordenador.TentarIniciar(folhas[0].No, folhas[1].No, primeiro.transform.position, segundo.transform.position), "O gesto global começa com dois elementos terminais anatômicos distintos", falhas);
                Exigir(global.Fator == 0f && referencia.Corresponde(raiz, folhas), "O fator 0 restaura exatamente o estado inicial", falhas);
                var distanciaPrimeiroGesto = Vector3.Distance(primeiro.transform.position, segundo.transform.position);
                var fatorPrimeiroGesto = global.Fator;
                VerificarFator(coordenador, global, primeiro, segundo, 0.2f, distanciaPrimeiroGesto, fatorPrimeiroGesto, ajuste, referencia, raiz, folhas, falhas);
                VerificarFator(coordenador, global, primeiro, segundo, 0.6f, distanciaPrimeiroGesto, fatorPrimeiroGesto, ajuste, referencia, raiz, folhas, falhas);
                VerificarFator(coordenador, global, primeiro, segundo, 1f, distanciaPrimeiroGesto, fatorPrimeiroGesto, ajuste, referencia, raiz, folhas, falhas);
                VerificarFator(coordenador, global, primeiro, segundo, 0.6f, distanciaPrimeiroGesto, fatorPrimeiroGesto, ajuste, referencia, raiz, folhas, falhas);
                VerificarFator(coordenador, global, primeiro, segundo, 0.2f, distanciaPrimeiroGesto, fatorPrimeiroGesto, ajuste, referencia, raiz, folhas, falhas);
                VerificarFator(coordenador, global, primeiro, segundo, 0f, distanciaPrimeiroGesto, fatorPrimeiroGesto, ajuste, referencia, raiz, folhas, falhas);
                Exigir(referencia.PosicoesFolhasERecursosCorrespondem(folhas), "Ao retornar o mesmo gesto a zero, toda a anatomia volta ao estado inicial", falhas);
                VerificarFator(coordenador, global, primeiro, segundo, 1f, distanciaPrimeiroGesto, fatorPrimeiroGesto, ajuste, referencia, raiz, folhas, falhas);
                Exigir(coordenador.Concluir(), "completion retains global factor", falhas);
                // Recapturar na pose retida não pode saltar; a redefinição restaura cada referência local.
                Exigir(coordenador.TentarIniciar(folhas[0].No, folhas[17].No, primeiro.transform.position, segundo.transform.position), "Uma nova captura aceita outros elementos terminais físicos", falhas);
                Exigir(Mathf.Abs(global.Fator - 1f) < Epsilon, "Uma nova captura mantém o fator aplicado anteriormente", falhas);
                var raizPostura = PosturaRaiz.Capturar(raiz.transform);
                var reagarrarDistancia = Vector3.Distance(primeiro.transform.position, segundo.transform.position);
                var reagarrarFator = global.Fator;
                VerificarFator(coordenador, global, primeiro, segundo, 0.6f, reagarrarDistancia, reagarrarFator, ajuste, referencia, raiz, folhas, falhas);
                VerificarFator(coordenador, global, primeiro, segundo, 0.2f, reagarrarDistancia, reagarrarFator, ajuste, referencia, raiz, folhas, falhas);
                VerificarFator(coordenador, global, primeiro, segundo, 0f, reagarrarDistancia, reagarrarFator, ajuste, referencia, raiz, folhas, falhas);
                Exigir(raizPostura.Corresponde(raiz.transform) && referencia.PosicoesFolhasERecursosCorrespondem(folhas), "A sequência de distância 1, 0,6, 0,2 e 0 preserva a raiz e restaura toda a anatomia", falhas);
                Exigir(coordenador.Concluir(), "zero-factor reverse transition completes", falhas);
                // A pose que o usuário dá a um grupo e a rotação de uma folha pertencem a ele: a transição
                // global de fator restaura só a posição local das folhas e nunca grava transforms de grupo
                // (ControladorExpansaoGlobal.AplicarFator).
                var ancestral = folhas[0].No.NoPai;
                if (ancestral != null)
                {
                    var ancestralPosicao = ancestral.transform.localPosition;
                    var ancestralRotacao = ancestral.transform.localRotation;
                    var folhaRotacao = folhas[0].transform.localRotation;
                    ancestral.transform.localPosition += new Vector3(0.07f, -0.02f, 0.04f);
                    ancestral.transform.Rotate(Vector3.up, 19f, Space.Self);
                    folhas[0].transform.Rotate(Vector3.right, 31f, Space.Self);
                    var ancestralPosicaoUsuario = ancestral.transform.localPosition;
                    var ancestralRotacaoUsuario = ancestral.transform.localRotation;
                    var folhaRotacaoUsuario = folhas[0].transform.localRotation;
                    Exigir(global.DefinirFator(0f), "O fator 0 aceita uma restauração com valores finitos", falhas);
                    Exigir((ancestral.transform.localPosition - ancestralPosicaoUsuario).sqrMagnitude < Epsilon * Epsilon && Quaternion.Angle(ancestral.transform.localRotation, ancestralRotacaoUsuario) < 0.001f && Quaternion.Angle(folhas[0].transform.localRotation, folhaRotacaoUsuario) < 0.001f, "O fator 0 preserva a pose dada pelo usuário ao grupo e a rotação da folha", falhas);
                    Exigir(referencia.RecursosEHierarquiaCorrespondem(folhas), "Malha e hierarquia inalteradas após a pose dada pelo usuário", falhas);
                    // Devolve a pose de teste para que as verificações seguintes partam da referência.
                    ancestral.transform.localPosition = ancestralPosicao;
                    ancestral.transform.localRotation = ancestralRotacao;
                    folhas[0].transform.localRotation = folhaRotacao;
                    Exigir(referencia.PosicoesFolhasERecursosCorrespondem(folhas), "No fator 0, as posições locais das folhas correspondem à referência", falhas);
                }

                Exigir(!global.DefinirFator(float.NaN) && !global.DefinirFator(float.PositiveInfinity), "non-finite factors are rejected without transform corruption", falhas);
                // Alterações na pose do ancestral/raiz não são sobrescritas por uma transição global de fator.
                raiz.transform.position += new Vector3(0.12f, 0.03f, -0.06f);
                raiz.transform.Rotate(Vector3.up, 23f, Space.World);
                raiz.transform.localScale = Vector3.Scale(raiz.transform.localScale, new Vector3(1.12f, 0.91f, 1.06f));
                primeiro.transform.position = raiz.transform.position;
                segundo.transform.position = primeiro.transform.position + Vector3.right * 0.18f;
                Exigir(coordenador.TentarIniciar(folhas[0].No, folhas[1].No, primeiro.transform.position, segundo.transform.position), "O gesto continua válido após a alteração da posição da raiz", falhas);
                var movidoRaizGestoDistancia = Vector3.Distance(primeiro.transform.position, segundo.transform.position);
                VerificarFator(coordenador, global, primeiro, segundo, 0.6f, movidoRaizGestoDistancia, global.Fator, ajuste, referencia, raiz, folhas, falhas);
                coordenador.CancelarERestaurar();
                Exigir(referencia.PosicoesFolhasERecursosCorrespondem(folhas), "Após a mudança de posição da raiz, o fator 0 continua restaurando exatamente o estado local inicial", falhas);
            }
            catch (Exception excecao)
            {
                falhas.Add("Exceção inesperada: " + excecao);
            }
            finally
            {
                coordenador?.CancelarERestaurar();
                referencia?.RestaurarRaiz(raiz);
                if (primeiro != null)
                    UnityEngine.Object.Destroy(primeiro);
                if (segundo != null)
                    UnityEngine.Object.Destroy(segundo);
            }

            if (falhas.Count == 0)
                Debug.Log("APROVADO: catálogo global com 240 elementos e 32 grupos; fatores 0/0,2/0,6/1; reversão, nova captura e redefinição; estabilidade da posição da raiz; preservação da malha e da hierarquia. A exclusividade da interface e o rastreamento físico das mãos ainda exigem uma verificação do operador com o XRI em execução.");
            else
                Debug.LogError("FAIL: global explode regression\n- " + string.Join("\n- ", falhas));
        }

        private static void VerificarFator(CoordenadorGestoAnatomico coordenador, ControladorExpansaoGlobal global, GameObject primeiro, GameObject segundo, float fator, float distanciaInicial, float fatorInicial, AjustesGesto ajuste, Instantaneo referencia, GameObject raiz, ElementoInteragivelMao[] folhas, List<string> falhas)
        {
            var raizAntes = PosturaRaiz.Capturar(raiz.transform);
            var fatorVariacao = fator - fatorInicial;
            var distanciaVariacao = Mathf.Abs(fatorVariacao) < Epsilon ? 0f : fatorVariacao * ajuste.VariacaoCompleta + Mathf.Sign(fatorVariacao) * ajuste.ZonaMorta;
            var distanciaAlvo = distanciaInicial + distanciaVariacao;
            segundo.transform.position = primeiro.transform.position + Vector3.right * distanciaAlvo;
            Exigir(coordenador.TentarAtualizar(primeiro.transform.position, segundo.transform.position), "fator atualizado " + fator, falhas);
            Exigir(Mathf.Abs(global.Fator - fator) < 0.001f, "fator contínuo esperado " + fator.ToString("F3") + ", actual " + global.Fator.ToString("F3") + " (start factor " + fatorInicial.ToString("F3") + ", start distance " + distanciaInicial.ToString("F3") + ", distância do alvo " + distanciaAlvo.ToString("F3") + ")", falhas);
            Exigir(raizAntes.Corresponde(raiz.transform), "factor " + fator + " não altera a transformação atual da raiz do SNC", falhas);
            Exigir(referencia.RecursosEHierarquiaCorrespondem(folhas), "malha e hierarquia inalteradas no fator " + fator, falhas);
            if (fator > 0f)
                Exigir(!referencia.PosicoesFolhasERecursosCorrespondem(folhas), "algum elemento terminal se move no fator " + fator, falhas);
        }

        private static void Exigir(bool condicao, string mensagem, List<string> falhas)
        {
            if (!condicao)
                falhas.Add(mensagem);
        }

        private readonly struct AjustesGesto
        {
            public readonly float ZonaMorta;
            public readonly float VariacaoCompleta;
            private AjustesGesto(float zonaMorta, float variacaoCompleta)
            {
                ZonaMorta = zonaMorta;
                VariacaoCompleta = variacaoCompleta;
            }

            public static AjustesGesto Ler(CoordenadorGestoAnatomico coordenador)
            {
                var serializado = new SerializedObject(coordenador);
                return new AjustesGesto(serializado.FindProperty("separacaoZonaMorta").floatValue, serializado.FindProperty("variacaoSeparacaoCompleta").floatValue);
            }
        }

        private sealed class Instantaneo
        {
            private readonly Vector3 posicaoRaiz;
            private readonly Quaternion rotacaoRaiz;
            private readonly Vector3 escalaRaiz;
            private readonly Entrada[] entradas;
            private readonly EntradaGrupo[] grupos;
            private Instantaneo(GameObject raiz, Entrada[] entradas, EntradaGrupo[] grupos)
            {
                posicaoRaiz = raiz.transform.position;
                rotacaoRaiz = raiz.transform.rotation;
                escalaRaiz = raiz.transform.localScale;
                this.entradas = entradas;
                this.grupos = grupos;
            }

            public static Instantaneo Capturar(GameObject raiz, ElementoInteragivelMao[] folhas)
            {
                var entradas = new Entrada[folhas.Length];
                for (var i = 0; i < folhas.Length; i++)
                    entradas[i] = new Entrada(folhas[i]);
                var todosNos = raiz.GetComponentsInChildren<NoInteracaoAnatomica>(true);
                var entradasGrupo = new List<EntradaGrupo>(ControladorExpansaoGlobal.QuantidadeGruposEsperada - 1);
                foreach (var no in todosNos)
                {
                    if (no != null && no.transform != raiz.transform && no.VinculoAnatomico != null && no.VinculoAnatomico.EhGrupo)
                        entradasGrupo.Add(new EntradaGrupo(no.transform));
                }

                if (entradasGrupo.Count != ControladorExpansaoGlobal.QuantidadeGruposEsperada - 1)
                    throw new InvalidOperationException("Eram esperados 31 nós de grupo que controlam posições descendentes; foram encontrados " + entradasGrupo.Count + ".");
                return new Instantaneo(raiz, entradas, entradasGrupo.ToArray());
            }

            public bool Corresponde(GameObject raiz, ElementoInteragivelMao[] folhas) => Vector3.Distance(raiz.transform.position, posicaoRaiz) < Epsilon && Quaternion.Angle(raiz.transform.rotation, rotacaoRaiz) < 0.001f && (raiz.transform.localScale - escalaRaiz).sqrMagnitude < Epsilon * Epsilon && PosicoesFolhasERecursosCorrespondem(folhas);
            public bool PosicoesFolhasERecursosCorrespondem(ElementoInteragivelMao[] folhas) => RecursosEHierarquiaCorrespondem(folhas) && TodasPosicoesLocaisCorrespondem() && PosicoesLocaisGruposCorrespondem();
            public bool RecursosEHierarquiaCorrespondem(ElementoInteragivelMao[] folhas)
            {
                if (folhas.Length != entradas.Length)
                    return false;
                foreach (var entrada in entradas)
                    if (!entrada.RecursosEHierarquiaCorrespondem())
                        return false;
                return true;
            }

            private bool TodasPosicoesLocaisCorrespondem()
            {
                foreach (var entrada in entradas)
                    if (!entrada.LocalCorresponde())
                        return false;
                return true;
            }

            private bool PosicoesLocaisGruposCorrespondem()
            {
                foreach (var grupo in grupos)
                    if (!grupo.LocalCorresponde())
                        return false;
                return true;
            }

            public void RestaurarRaiz(GameObject raiz)
            {
                if (raiz == null)
                    return;
                raiz.transform.SetPositionAndRotation(posicaoRaiz, rotacaoRaiz);
                raiz.transform.localScale = escalaRaiz;
            }
        }

        private sealed class Entrada
        {
            private readonly ElementoInteragivelMao folha;
            private readonly Transform pai;
            private readonly Vector3 local;
            private readonly Quaternion rotacao;
            private readonly Vector3 escala;
            private readonly MeshCollider colisor;
            private readonly Mesh malha;
            private readonly MeshFilter[] filtros;
            private readonly Mesh[] filtroMalhas;
            public Entrada(ElementoInteragivelMao folha)
            {
                this.folha = folha;
                pai = folha.transform.parent;
                local = folha.transform.localPosition;
                rotacao = folha.transform.localRotation;
                escala = folha.transform.localScale;
                colisor = folha.colliders.Count == 1 ? folha.colliders[0] as MeshCollider : null;
                malha = colisor != null ? colisor.sharedMesh : null;
                filtros = folha.GetComponentsInChildren<MeshFilter>(true);
                filtroMalhas = new Mesh[filtros.Length];
                for (var i = 0; i < filtros.Length; i++)
                    filtroMalhas[i] = filtros[i] != null ? filtros[i].sharedMesh : null;
            }

            public bool LocalCorresponde() => folha != null && (folha.transform.localPosition - local).sqrMagnitude < Epsilon * Epsilon && Quaternion.Angle(folha.transform.localRotation, rotacao) < 0.001f && (folha.transform.localScale - escala).sqrMagnitude < Epsilon * Epsilon;
            public bool RecursosEHierarquiaCorrespondem()
            {
                if (folha == null || folha.transform.parent != pai || colisor == null || colisor.sharedMesh != malha || filtros.Length != filtroMalhas.Length)
                    return false;
                for (var i = 0; i < filtros.Length; i++)
                    if (filtros[i] == null || filtros[i].sharedMesh != filtroMalhas[i])
                        return false;
                return true;
            }
        }

        private sealed class EntradaGrupo
        {
            private readonly Transform grupo;
            private readonly Transform pai;
            private readonly Vector3 local;
            private readonly Quaternion rotacao;
            private readonly Vector3 escala;
            public EntradaGrupo(Transform grupo)
            {
                this.grupo = grupo;
                pai = grupo.parent;
                local = grupo.localPosition;
                rotacao = grupo.localRotation;
                escala = grupo.localScale;
            }

            public bool LocalCorresponde() => grupo != null && grupo.parent == pai && (grupo.localPosition - local).sqrMagnitude < Epsilon * Epsilon && Quaternion.Angle(grupo.localRotation, rotacao) < 0.001f && (grupo.localScale - escala).sqrMagnitude < Epsilon * Epsilon;
        }

        private readonly struct PosturaRaiz
        {
            private readonly Vector3 posicao;
            private readonly Quaternion rotacao;
            private readonly Vector3 escala;
            private PosturaRaiz(Transform raiz)
            {
                posicao = raiz.position;
                rotacao = raiz.rotation;
                escala = raiz.localScale;
            }

            public static PosturaRaiz Capturar(Transform raiz) => new(raiz);
            public bool Corresponde(Transform raiz) => Vector3.Distance(posicao, raiz.position) < Epsilon && Quaternion.Angle(rotacao, raiz.rotation) < .001f && (escala - raiz.localScale).sqrMagnitude < Epsilon * Epsilon;
        }
    }
}
