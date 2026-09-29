using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Valdorso.Movement;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Mette nell'Animator del giocatore le tre animazioni degli ostacoli, senza doverle collegare a mano:
    /// 1) imposta l'importazione dei file Mixamo (Humanoid, non in ciclo; rotazione cotta nella posa,
    ///    altezza e spostamento no, Y basata sui piedi: come climbing e jump_over);
    /// 2) aggiunge il parametro ClimbHigh (Trigger);
    /// 3) nel livello Combattimento: Scavalca usa vault_over_box, Sale usa climb_low,
    ///    e crea Arrampica con freehang_climb (da Any State con ClimbHigh, poi torna a Nessuna);
    /// 4) regola la velocità di ogni stato perché l'animazione duri quanto il movimento
    ///    (i tempi si leggono da ObstacleTraversal sul prefab Giocatore_UMA).
    /// Per provare un'altra animazione basta cambiare il nome del file nella tabella qui sotto e rilanciare.
    /// Si può rilanciare: quello che c'è già non viene duplicato.
    /// </summary>
    public static class AnimazioniOstacoli
    {
        const string Cartella = "Assets/_Valdorso/Art/Animazioni/Comuni/";
        const string Controller = "Assets/_Valdorso/Art/Animazioni/Controller/Giocatore_Animator.controller";
        const string Prefab = "Assets/_Valdorso/Prefabs/Giocatore_UMA.prefab";
        const string Livello = "Combattimento";
        const string Riposo = "Nessuna";

        // file Mixamo (senza .fbx) → stato del livello Combattimento → trigger
        static readonly (string file, string stato, string trigger)[] Mosse =
        {
            ("vault_over_box", "Scavalca", "Vault"),
            ("climb_low", "Sale", "Climb"),
            ("freehang_climb", "Arrampica", "ClimbHigh"),
        };

        [MenuItem("Valdorso/Animazioni/Aggiungi le animazioni degli ostacoli")]
        static void Aggiungi()
        {
            var ot = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab)?.GetComponent<ObstacleTraversal>();
            if (ot == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Non trovo ObstacleTraversal sul prefab:\n" + Prefab, "OK");
                return;
            }
            float[] durate = { ot.DurataScavalca, ot.DurataSale, ot.DurataArrampica };

            var clip = new AnimationClip[Mosse.Length];
            for (int i = 0; i < Mosse.Length; i++)
            {
                string percorso = Cartella + Mosse[i].file + ".fbx";
                if (!PreparaImportazione(percorso, Mosse[i].file))
                {
                    EditorUtility.DisplayDialog("Valdorso", "Non trovo " + percorso +
                        "\n\nServono vault_over_box, climb_low e freehang_climb (.fbx) in quella cartella.", "OK");
                    return;
                }
                clip[i] = AssetDatabase.LoadAllAssetsAtPath(percorso).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip[i] == null)
                {
                    EditorUtility.DisplayDialog("Valdorso", "Nel file " + Mosse[i].file + " non c'è un'animazione.", "OK");
                    return;
                }
            }

            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if (ac == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Non trovo l'Animator del giocatore:\n" + Controller, "OK");
                return;
            }
            Undo.RecordObject(ac, "Animazioni degli ostacoli");

            if (!ac.parameters.Any(p => p.name == "ClimbHigh"))
                ac.AddParameter("ClimbHigh", AnimatorControllerParameterType.Trigger);

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

            string rapporto = "";
            for (int i = 0; i < Mosse.Length; i++)
            {
                AnimatorState s = sm.states.Select(x => x.state).FirstOrDefault(x => x.name == Mosse[i].stato)
                                  ?? sm.AddState(Mosse[i].stato, new Vector3(520, 590 + 70 * i, 0));
                s.motion = clip[i];
                s.writeDefaultValues = false; // come gli altri stati del livello Combattimento
                s.speed = Mathf.Max(0.1f, clip[i].length / Mathf.Max(0.1f, durate[i]));

                // Entrata: da Any State col suo trigger, da vivi, subito
                AnimatorStateTransition entra = sm.anyStateTransitions.FirstOrDefault(t => t.destinationState == s)
                                                ?? sm.AddAnyStateTransition(s);
                entra.hasExitTime = false;
                entra.duration = 0.05f;
                entra.hasFixedDuration = true;
                entra.canTransitionToSelf = false;
                entra.conditions = new AnimatorCondition[0];
                entra.AddCondition(AnimatorConditionMode.If, 0f, Mosse[i].trigger);
                entra.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");

                // Uscita: a fine animazione si torna a Nessuna (il livello si spegne da solo)
                AnimatorStateTransition esce = s.transitions.FirstOrDefault(t => t.destinationState == riposo)
                                               ?? s.AddTransition(riposo);
                esce.hasExitTime = true;
                esce.exitTime = 0.9f;
                esce.duration = 0.15f;
                esce.hasFixedDuration = true;
                esce.conditions = new AnimatorCondition[0];

                rapporto += $"{Mosse[i].stato}: {Mosse[i].file}, {clip[i].length:0.00} s → {durate[i]:0.00} s (velocità {s.speed:0.00})\n";
            }

            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            Debug.Log("[Valdorso] Animazioni degli ostacoli:\n" + rapporto);
            EditorUtility.DisplayDialog("Valdorso", "Animazioni degli ostacoli aggiunte.\n\n" + rapporto +
                "\nRicorda: File → Save Project.", "OK");
        }

        /// <summary>Humanoid, non in ciclo; rotazione cotta (Original), altezza sui piedi e spostamento non cotti.</summary>
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
            c.loopTime = false;
            c.lockRootRotation = true;        // Root Transform Rotation: Bake Into Pose
            c.keepOriginalOrientation = true; // Based Upon: Original
            c.lockRootHeightY = false;        // Root Transform Position (Y): non cotta...
            c.keepOriginalPositionY = false;
            c.heightFromFeet = true;          // ...Based Upon: Feet
            c.lockRootPositionXZ = false;     // Root Transform Position (XZ): non cotta
            c.keepOriginalPositionXZ = true;
            imp.clipAnimations = new[] { c };
            imp.SaveAndReimport();
            return true;
        }
    }
}
