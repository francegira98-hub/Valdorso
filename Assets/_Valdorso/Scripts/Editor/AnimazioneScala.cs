using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Valdorso.Interazione;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Mette nell'Animator del giocatore l'animazione della scala a pioli:
    /// 1) imposta l'importazione di ladder_climb (Humanoid, in ciclo; rotazione e spostamento orizzontale cotti (orizzontale sul centro del corpo),
    ///    altezza non cotta: così l'animazione resta sul posto e a salire ci pensa il codice);
    /// 2) aggiunge i parametri SuScala (Bool) e VelocitaScala (Float);
    /// 3) nel livello Combattimento crea lo stato Scala, con i passaggi da e verso Nessuna; la sua velocità
    ///    è moltiplicata da VelocitaScala (1 sale, -1 scende all'indietro, 0 fermo sui pioli) e tarata
    ///    sulla velocità di salita di Postura (prefab Giocatore_UMA).
    /// Si può rilanciare: quello che c'è già non viene duplicato.
    /// </summary>
    public static class AnimazioneScala
    {
        const string File = "Assets/_Valdorso/Art/Animazioni/Comuni/ladder_climb.fbx";
        const string Controller = "Assets/_Valdorso/Art/Animazioni/Controller/Giocatore_Animator.controller";
        const string Prefab = "Assets/_Valdorso/Prefabs/Giocatore_UMA.prefab";
        const string Livello = "Combattimento";
        const string Riposo = "Nessuna";

        [MenuItem("Valdorso/Animazioni/Aggiungi la scala a pioli")]
        static void Aggiungi()
        {
            var postura = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab)?.GetComponent<Postura>();
            if (postura == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Non trovo Postura sul prefab:\n" + Prefab, "OK");
                return;
            }

            var imp = AssetImporter.GetAtPath(File) as ModelImporter;
            if (imp == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Non trovo " + File, "OK");
                return;
            }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            ModelImporterClipAnimation[] clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
            if (clips.Length == 0)
            {
                EditorUtility.DisplayDialog("Valdorso", "Nel file ladder_climb non c'è un'animazione.", "OK");
                return;
            }
            ModelImporterClipAnimation c = clips[0];
            c.name = "ladder_climb";
            c.loopTime = true;
            c.loopPose = false;
            c.lockRootRotation = true;        // rotazione: Bake Into Pose...
            c.keepOriginalOrientation = false; // ...Based Upon: Body Orientation. Con "Original" il corpo della clip
                                               // di Mixamo guarda all'indietro: così guarda sempre dove guarda il personaggio
            c.lockRootHeightY = false;        // altezza: non cotta (la salita la fa il codice)
            c.keepOriginalPositionY = true;
            c.heightFromFeet = false;
            c.lockRootPositionXZ = true;      // orizzontale: cotta, resta attaccato alla scala...
            c.keepOriginalPositionXZ = false; // ...ma sul centro del corpo (Center of Mass): la clip di Mixamo
                                              // ha il corpo spostato di lato rispetto all'origine
            imp.clipAnimations = new[] { c };
            imp.SaveAndReimport();

            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(File).OfType<AnimationClip>()
                .FirstOrDefault(x => !x.name.StartsWith("__preview__"));
            if (clip == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Nel file ladder_climb non c'è un'animazione.", "OK");
                return;
            }

            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if (ac == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Non trovo l'Animator del giocatore:\n" + Controller, "OK");
                return;
            }
            Undo.RecordObject(ac, "Scala a pioli");
            if (!ac.parameters.Any(p => p.name == "SuScala")) ac.AddParameter("SuScala", AnimatorControllerParameterType.Bool);
            if (!ac.parameters.Any(p => p.name == "VelocitaScala"))
                ac.AddParameter(new AnimatorControllerParameter { name = "VelocitaScala", type = AnimatorControllerParameterType.Float, defaultFloat = 0f });

            AnimatorControllerLayer livello = ac.layers.FirstOrDefault(l => l.name == Livello);
            AnimatorStateMachine sm = livello?.stateMachine;
            AnimatorState riposo = sm?.states.Select(s => s.state).FirstOrDefault(s => s.name == Riposo);
            if (riposo == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Nell'Animator manca il livello " + Livello + " o il suo stato " + Riposo + ".", "OK");
                return;
            }

            AnimatorState scala = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Scala")
                                  ?? sm.AddState("Scala", new Vector3(520, 800, 0));
            scala.motion = clip;
            scala.writeDefaultValues = false;
            scala.speedParameterActive = true;
            scala.speedParameter = "VelocitaScala";

            // Quanto sale l'animazione da sola, per farla andare al passo con il codice
            float salitaClip = Mathf.Abs(clip.averageSpeed.y);
            scala.speed = salitaClip > 0.05f ? postura.VelocitaScala / salitaClip : 1f;

            AnimatorStateTransition entra = riposo.transitions.FirstOrDefault(t => t.destinationState == scala) ?? riposo.AddTransition(scala);
            entra.hasExitTime = false;
            entra.duration = 0.2f;
            entra.hasFixedDuration = true;
            entra.conditions = new AnimatorCondition[0];
            entra.AddCondition(AnimatorConditionMode.If, 0f, "SuScala");
            entra.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");

            AnimatorStateTransition esce = scala.transitions.FirstOrDefault(t => t.destinationState == riposo) ?? scala.AddTransition(riposo);
            esce.hasExitTime = false;
            esce.duration = 0.25f;
            esce.hasFixedDuration = true;
            esce.conditions = new AnimatorCondition[0];
            esce.AddCondition(AnimatorConditionMode.IfNot, 0f, "SuScala");

            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            string rapporto = $"ladder_climb: {clip.length:0.00} s, sale da sola {salitaClip:0.00} m/s → velocità dello stato {scala.speed:0.00} " +
                              $"(salita del personaggio {postura.VelocitaScala:0.00} m/s).";
            Debug.Log("[Valdorso] Scala a pioli: " + rapporto);
            EditorUtility.DisplayDialog("Valdorso", "Scala a pioli aggiunta all'Animator.\n\n" + rapporto + "\n\nRicorda: File → Save Project.", "OK");
        }
    }
}