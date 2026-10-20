using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace VRSNC.Interacao
{
    /// <summary>
    /// Leituras comuns das mãos rastreadas: o subsistema do XR Hands em execução e o ponto físico da pinça (meio entre as
    /// pontas do polegar e do indicador) no mundo. Usado pela <see cref="PonteInteracaoMao"/> e pelos gestos no ar.
    /// </summary>
    public static class PincaMao
    {
        private static readonly List<XRHandSubsystem> Subsistemas = new();

        /// <summary>Subsistema de mãos em execução; <paramref name="cache"/> evita procurar a cada quadro.</summary>
        public static bool TentarObterSubsistema(ref XRHandSubsystem cache, out XRHandSubsystem subsistema)
        {
            if (cache != null && cache.running)
            {
                subsistema = cache;
                return true;
            }

            Subsistemas.Clear();
            SubsystemManager.GetSubsystems(Subsistemas);
            for (var indice = 0; indice < Subsistemas.Count; indice++)
            {
                if (Subsistemas[indice] != null && Subsistemas[indice].running)
                {
                    cache = Subsistemas[indice];
                    subsistema = cache;
                    return true;
                }
            }

            cache = null;
            subsistema = null;
            return false;
        }

        /// <summary>
        /// Ponto da pinça no mundo. As poses das articulações vêm no espaço da XR Origin, por isso a conversão pela
        /// <paramref name="origemXR"/>. Falso sem rastreamento ou sem as duas pontas.
        /// </summary>
        public static bool TentarObterPonto(XRHand mao, Transform origemXR, out Vector3 ponto)
        {
            ponto = default;
            if (!mao.isTracked || origemXR == null)
                return false;
            if (!mao.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var polegar) || !mao.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var indicador))
                return false;
            ponto = origemXR.TransformPoint((polegar.position + indicador.position) * 0.5f);
            return true;
        }
    }
}
