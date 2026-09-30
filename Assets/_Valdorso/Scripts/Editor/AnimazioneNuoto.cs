using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Mette nell'Animator del giocatore il nuoto in superficie:
    /// 1) imposta l'importazione di swim_idle (stare a galla) e swim (bracciata): Humanoid, in ciclo;
    ///    rotazione e altezza cotte nella posa (Body Orientation), spostamento in avanti non cotto
    ///    (così la bracciata resta sul posto e ad avanzare ci pensa il codice);
    /// 2) aggiunge i parametri InAcqua (Bool) e VelocitaNuoto (Float);
    /// 3) nel livello Combattimento crea lo stato Nuoto, che mescola le due animazioni secondo VelocitaNuoto
    ///    (0 fermi a galla, 1 bracciata), con i passaggi da e verso Nessuna.
    /// Si può rilanciare: quello che c'è già non viene duplicato.
    /// </summary>
    public static class AnimazioneNuoto
    {
        const string Cartella = "Assets/_Valdorso/Art/Animazioni/Comuni/";
        const string Controller = "Assets/_Valdorso/Art/Animazioni/Controller/Giocatore_Animator.controller";
        const string Livello = "Combattimento";
        const string Riposo = "Nessuna";

        [MenuItem("Valdorso/Animazioni/Aggiungi il nuoto")]
        static void Aggiungi()
        {
            AnimationClip fermo = Prepara("swim_idle");
            AnimationClip bracciata = Prepara("swim");
            if (fermo == null || bracciata == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Servono swim_idle.fbx e swim.fbx in " + Cartella, "OK");
                return;
            }

            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            AnimatorControllerLayer livello = ac?.layers.FirstOrDefault(l => l.name == Livello);
            AnimatorStateMachine sm = livello?.stateMachine;
            AnimatorState riposo = sm?.states.Select(s => s.state).FirstOrDefault(s => s.name == Riposo);
            if (riposo == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Non trovo l'Animator del giocatore, il livello " + Livello + " o lo stato " + Riposo + ".", "OK");
                return;
            }
            Undo.RecordObject(ac, "Nuoto");
            if (!ac.parameters.Any(p => p.name == "InAcqua")) ac.AddParameter("InAcqua", AnimatorControllerParameterType.Bool);
            if (!ac.parameters.Any(p => p.name == "VelocitaNuoto"))
                ac.AddParameter(new AnimatorControllerParameter { name = "VelocitaNuoto", type = AnimatorControllerParameterType.Float, defaultFloat = 0f });

            AnimatorState nuoto = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Nuoto")
                                  ?? sm.AddState("Nuoto", new Vector3(520, 870, 0));

            // La miscela: una sola, dentro il file dell'Animator (se c'è già la si riusa)
            BlendTree miscela = nuoto.motion as BlendTree;
            if (miscela == null)
            {
                miscela = new BlendTree { name = "Nuoto", hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(miscela, ac);
            }
            miscela.blendType = BlendTreeType.Simple1D;
            miscela.blendParameter = "VelocitaNuoto";
            miscela.useAutomaticThresholds = false;
            miscela.children = new ChildMotion[0];
            miscela.AddChild(fermo, 0f);
            miscela.AddChild(bracciata, 1f);
            nuoto.motion = miscela;
            nuoto.writeDefaultValues = false;

            AnimatorStateTransition entra = riposo.transitions.FirstOrDefault(t => t.destinationState == nuoto) ?? riposo.AddTransition(nuoto);
            entra.hasExitTime = false;
            entra.duration = 0.35f;
            entra.hasFixedDuration = true;
            entra.conditions = new AnimatorCondition[0];
            entra.AddCondition(AnimatorConditionMode.If, 0f, "InAcqua");
            entra.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");

            AnimatorStateTransition esce = nuoto.transitions.FirstOrDefault(t => t.destinationState == riposo) ?? nuoto.AddTransition(riposo);
            esce.hasExitTime = false;
            esce.duration = 0.35f;
            esce.hasFixedDuration = true;
            esce.conditions = new AnimatorCondition[0];
            esce.AddCondition(AnimatorConditionMode.IfNot, 0f, "InAcqua");

            EditorUtility.SetDirty(miscela);
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            string rapporto = $"swim_idle {fermo.length:0.00} s, swim {bracciata.length:0.00} s.";
            Debug.Log("[Valdorso] Nuoto aggiunto all'Animator: " + rapporto);
            EditorUtility.DisplayDialog("Valdorso", "Nuoto aggiunto all'Animator.\n\n" + rapporto + "\n\nRicorda: File → Save Project.", "OK");
        }

        static AnimationClip Prepara(string nome)
        {
            string percorso = Cartella + nome + ".fbx";
            var imp = AssetImporter.GetAtPath(percorso) as ModelImporter;
            if (imp == null) return null;
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            ModelImporterClipAnimation[] clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
            if (clips.Length == 0) return null;
            ModelImporterClipAnimation c = clips[0];
            c.name = nome;
            c.loopTime = true;
            c.loopPose = false;
            c.lockRootRotation = true;         // rotazione cotta, sul corpo
            c.keepOriginalOrientation = false;
            c.lockRootHeightY = true;          // altezza cotta (l'ondeggiare resta nella posa)
            c.keepOriginalPositionY = true;
            c.heightFromFeet = false;
            c.lockRootPositionXZ = false;      // avanti: non cotto, la bracciata resta sul posto
            c.keepOriginalPositionXZ = false;
            imp.clipAnimations = new[] { c };
            imp.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(percorso).OfType<AnimationClip>()
                .FirstOrDefault(x => !x.name.StartsWith("__preview__"));
        }
    }
}