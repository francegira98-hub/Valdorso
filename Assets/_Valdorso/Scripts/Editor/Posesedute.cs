using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Mette nell'Animator del giocatore le pose da seduti e da sdraiati, senza doverle collegare a mano:
    /// 1) imposta l'importazione dei tre file Mixamo (Humanoid, in ciclo, radice "cotta" nella posa, come le altre animazioni);
    /// 2) aggiunge i parametri Seduto, Sdraiato e Femminile (Bool);
    /// 3) nel livello Combattimento crea gli stati Seduto_M, Seduta_F e Sdraiato, con i passaggi da e verso "Nessuna".
    /// I file devono chiamarsi Seduto_M, Seduta_F e Sdraiato (.fbx) e stare in Assets/_Valdorso/Art/Animazioni/Comuni.
    /// Si può rilanciare: quello che c'è già non viene duplicato.
    /// </summary>
    public static class PoseSedute
    {
        const string Cartella = "Assets/_Valdorso/Art/Animazioni/Comuni/";
        const string Controller = "Assets/_Valdorso/Art/Animazioni/Controller/Giocatore_Animator.controller";
        const string Livello = "Combattimento";
        const string Riposo = "Nessuna";
        const float Dissolvenza = 0.35f;

        [MenuItem("Valdorso/Animazioni/Aggiungi le pose da seduti")]
        static void Aggiungi()
        {
            string[] nomi = { "Seduto_M", "Seduta_F", "Sdraiato" };
            var clip = new AnimationClip[nomi.Length];
            for (int i = 0; i < nomi.Length; i++)
            {
                string percorso = Cartella + nomi[i] + ".fbx";
                if (!PreparaImportazione(percorso, nomi[i]))
                {
                    EditorUtility.DisplayDialog("Valdorso", "Non trovo " + percorso +
                        "\n\nMetti i tre file Mixamo in quella cartella con questi nomi: Seduto_M, Seduta_F, Sdraiato.", "OK");
                    return;
                }
                clip[i] = AssetDatabase.LoadAllAssetsAtPath(percorso).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip[i] == null)
                {
                    EditorUtility.DisplayDialog("Valdorso", "Nel file " + nomi[i] + " non c'è un'animazione.", "OK");
                    return;
                }
            }

            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if (ac == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Non trovo l'Animator del giocatore:\n" + Controller, "OK");
                return;
            }
            Undo.RecordObject(ac, "Pose da seduti");

            AggiungiParametro(ac, "Seduto");
            AggiungiParametro(ac, "Sdraiato");
            AggiungiParametro(ac, "Femminile");

            AnimatorControllerLayer livello = ac.layers.FirstOrDefault(l => l.name == Livello);
            if (livello == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Nell'Animator manca il livello " + Livello + ".", "OK");
                return;
            }
            AnimatorStateMachine sm = livello.stateMachine;
            AnimatorState riposo = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == Riposo);
            if (riposo == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Nel livello " + Livello + " manca lo stato " + Riposo + ".", "OK");
                return;
            }

            // Seduto_M: Seduto e non Femminile · Seduta_F: Seduto e Femminile · Sdraiato: Sdraiato
            AnimatorState m = Stato(sm, "Seduto_M", clip[0], new Vector3(520, 380, 0));
            AnimatorState f = Stato(sm, "Seduta_F", clip[1], new Vector3(520, 450, 0));
            AnimatorState d = Stato(sm, "Sdraiato", clip[2], new Vector3(520, 520, 0));

            Passaggio(riposo, m, ("Seduto", AnimatorConditionMode.If), ("Femminile", AnimatorConditionMode.IfNot), ("Dead", AnimatorConditionMode.IfNot));
            Passaggio(riposo, f, ("Seduto", AnimatorConditionMode.If), ("Femminile", AnimatorConditionMode.If), ("Dead", AnimatorConditionMode.IfNot));
            Passaggio(riposo, d, ("Sdraiato", AnimatorConditionMode.If), ("Dead", AnimatorConditionMode.IfNot));
            Passaggio(m, riposo, ("Seduto", AnimatorConditionMode.IfNot));
            Passaggio(f, riposo, ("Seduto", AnimatorConditionMode.IfNot));
            Passaggio(d, riposo, ("Sdraiato", AnimatorConditionMode.IfNot));

            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            Debug.Log("[Valdorso] Pose da seduti aggiunte all'Animator del giocatore.");
            EditorUtility.DisplayDialog("Valdorso", "Pose aggiunte: Seduto_M, Seduta_F, Sdraiato.\n\nRicorda: File → Save Project.", "OK");
        }

        /// <summary>Humanoid, in ciclo, radice cotta nella posa e altezza originale: come le altre animazioni Mixamo del progetto.</summary>
        static bool PreparaImportazione(string percorso, string nomeClip)
        {
            var imp = AssetImporter.GetAtPath(percorso) as ModelImporter;
            if (imp == null) return false;

            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;

            ModelImporterClipAnimation[] clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
            if (clips.Length == 0) return false;
            ModelImporterClipAnimation c = clips[0];
            c.name = nomeClip;
            c.loopTime = true;
            c.loopPose = false;
            c.lockRootRotation = true;        // Bake Into Pose
            c.lockRootHeightY = true;
            c.lockRootPositionXZ = true;
            c.keepOriginalOrientation = true; // Based Upon: Original
            c.keepOriginalPositionY = true;
            c.keepOriginalPositionXZ = true;
            c.heightFromFeet = false;
            imp.clipAnimations = new[] { c };
            imp.SaveAndReimport();
            return true;
        }

        static void AggiungiParametro(AnimatorController ac, string nome)
        {
            if (ac.parameters.Any(p => p.name == nome)) return;
            ac.AddParameter(nome, AnimatorControllerParameterType.Bool);
        }

        static AnimatorState Stato(AnimatorStateMachine sm, string nome, AnimationClip clip, Vector3 posizione)
        {
            AnimatorState s = sm.states.Select(x => x.state).FirstOrDefault(x => x.name == nome);
            if (s == null) s = sm.AddState(nome, posizione);
            s.motion = clip;
            s.writeDefaultValues = false; // come gli altri stati del livello Combattimento
            return s;
        }

        /// <summary>Un passaggio con dissolvenza, senza aspettare la fine dell'animazione. Se c'è già, rimette le condizioni.</summary>
        static void Passaggio(AnimatorState da, AnimatorState a, params (string p, AnimatorConditionMode modo)[] condizioni)
        {
            AnimatorStateTransition t = da.transitions.FirstOrDefault(x => x.destinationState == a) ?? da.AddTransition(a);
            t.hasExitTime = false;
            t.duration = Dissolvenza;
            t.hasFixedDuration = true;
            t.canTransitionToSelf = false;
            t.conditions = new AnimatorCondition[0];
            foreach (var (p, modo) in condizioni) t.AddCondition(modo, 0f, p);
        }
    }
}
