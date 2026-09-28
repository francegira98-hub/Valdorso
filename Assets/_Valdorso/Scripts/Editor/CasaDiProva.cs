using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Monta tre case di prova con i pezzi del villaggio Hivemind, in una scena nuova non salvata,
    /// per verificare le regole dei perni prima di scrivere lo strumento del villaggio.
    /// Regole (dai perni): muri con il perno a un'estremità, lunghi verso +X, alti 2,5 m;
    /// pavimento e tetti con il perno nello stesso angolo, da X 0 a 6 e da Z 0 a -6.
    /// Rotazioni intorno a Y: 90 = il pezzo corre verso -Z, 180 = verso -X, 270 = verso +Z.
    /// Menu: Valdorso → Villaggio → Case di prova (scena nuova). Solo editor.
    /// </summary>
    public static class CasaDiProva
    {
        const float Modulo = 6f;        // lato di un modulo di pavimento e di tetto
        const float AltezzaPiano = 2.5f; // altezza dei muri
        const string CartellaKit = "Assets/HIVEMIND";

        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
        static readonly List<string> mancanti = new List<string>();

        [MenuItem("Valdorso/Villaggio/Case di prova (scena nuova)")]
        static void Crea()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            cache.Clear();
            mancanti.Clear();

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Un suolo piatto per vedere come appoggiano le case (80 × 80 m).
            GameObject suolo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            suolo.name = "Suolo";
            suolo.transform.position = new Vector3(20f, -0.01f, -3f);
            suolo.transform.localScale = new Vector3(8f, 1f, 8f);

            Transform radice = new GameObject("CaseDiProva").transform;
            Casa(radice, "A_6x6_tetto_intero", new Vector3(0f, 0f, 0f), 1, 1, true);
            Casa(radice, "B_6x12_due_testate", new Vector3(12f, 0f, 0f), 2, 1, false);
            Casa(radice, "C_6x12_due_piani", new Vector3(30f, 0f, 0f), 2, 2, false);

            Selection.activeTransform = radice;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();

            if (mancanti.Count > 0)
                Debug.LogWarning("[Valdorso] Pezzi non trovati in " + CartellaKit + ": " + string.Join(", ", mancanti));
            Debug.Log("[Valdorso] Case di prova montate: A 6x6 con tetto intero, B 6x12 con due testate, C 6x12 su due piani. La scena non è salvata.");
        }

        /// <summary>Una casa lunga 'moduli' × 6 m e profonda 6 m, con 'piani' piani; l'angolo del perno è 'origine'.</summary>
        static void Casa(Transform radice, string nome, Vector3 origine, int moduli, int piani, bool tettoIntero)
        {
            Transform casa = new GameObject(nome).transform;
            casa.SetParent(radice, false);
            casa.localPosition = origine;

            float L = moduli * Modulo;  // lunghezza lungo X
            float P = Modulo;           // profondità lungo -Z

            for (int p = 0; p < piani; p++)
            {
                float y = p * AltezzaPiano;
                bool terra = p == 0;

                // Pavimento
                for (int i = 0; i < moduli; i++)
                    Pezzo(casa, "SM_floor6m_a1", new Vector3(i * Modulo, y, 0f), 0f);

                // Lato lungo davanti (Z = 0), da X 0 verso +X: al piano terra porta + finestra grande
                for (int i = 0; i < moduli; i++)
                {
                    float x = i * Modulo;
                    if (terra && i == 0)
                    {
                        Pezzo(casa, "SM_wallDoor2m_a1", new Vector3(x, y, 0f), 0f);
                        Pezzo(casa, "SM_wallWindow4m_a1", new Vector3(x + 2f, y, 0f), 0f);
                    }
                    else
                    {
                        Pezzo(casa, "SM_wallWindow3m_a1", new Vector3(x, y, 0f), 0f);
                        Pezzo(casa, "SM_wall3m_a1", new Vector3(x + 3f, y, 0f), 0f);
                    }
                }

                // Lato lungo dietro (Z = -6), girato di 180: parte da X+3 e corre verso -X
                for (int i = 0; i < moduli; i++)
                {
                    float x = i * Modulo;
                    Pezzo(casa, "SM_wall3m_a1", new Vector3(x + 3f, y, -P), 180f);
                    Pezzo(casa, "SM_wallWindow3m_a1", new Vector3(x + 6f, y, -P), 180f);
                }

                // Lato corto sinistro (X = 0), girato di 90: da Z 0 verso -Z
                Pezzo(casa, "SM_wall3m_a1", new Vector3(0f, y, 0f), 90f);
                Pezzo(casa, "SM_wallWindow3m_a1", new Vector3(0f, y, -3f), 90f);

                // Lato corto destro (X = L), girato di 270: da Z -6 verso +Z
                Pezzo(casa, "SM_wall3m_a1", new Vector3(L, y, -P), 270f);
                Pezzo(casa, "SM_wallWindow3m_a1", new Vector3(L, y, -3f), 270f);

                // Travi d'angolo, una per angolo con le quattro rotazioni (per vedere il verso giusto)
                Pezzo(casa, "SM_planksCorner_a1", new Vector3(0f, y, 0f), 0f);
                Pezzo(casa, "SM_planksCorner_a1", new Vector3(L, y, 0f), 270f);
                Pezzo(casa, "SM_planksCorner_a1", new Vector3(L, y, -P), 180f);
                Pezzo(casa, "SM_planksCorner_a1", new Vector3(0f, y, -P), 90f);
            }

            float yTetto = piani * AltezzaPiano;

            // Timpani sopra i lati corti
            Pezzo(casa, "SM_wallTopRoof6m_a1", new Vector3(0f, yTetto, 0f), 90f);
            Pezzo(casa, "SM_wallTopRoof6m_a1", new Vector3(L, yTetto, -P), 270f);

            // Tetto
            if (tettoIntero && moduli == 1)
            {
                Pezzo(casa, "SM_roofTop6mx6m_a1", new Vector3(0f, yTetto, 0f), 0f);
            }
            else
            {
                Pezzo(casa, "SM_roofEnd6m_a1", new Vector3(0f, yTetto, 0f), 0f);
                for (int i = 1; i < moduli - 1; i++)
                    Pezzo(casa, "SM_roofMiddle6m_a1", new Vector3(i * Modulo, yTetto, 0f), 0f);
                Pezzo(casa, "SM_roofEnd6m_a1", new Vector3(L, yTetto, -P), 180f);
            }
        }

        /// <summary>Posa un prefab del kit (collegato al prefab, non una copia) nella posizione locale data.</summary>
        static void Pezzo(Transform casa, string nomePrefab, Vector3 posizione, float rotY)
        {
            GameObject prefab = Carica(nomePrefab);
            if (prefab == null) return;

            var pezzo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, casa);
            pezzo.transform.localPosition = posizione;
            pezzo.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        }

        static GameObject Carica(string nome)
        {
            if (cache.TryGetValue(nome, out GameObject trovato)) return trovato;

            GameObject prefab = null;
            foreach (string guid in AssetDatabase.FindAssets(nome + " t:Prefab", new[] { CartellaKit }))
            {
                string percorso = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(percorso) != nome) continue;
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(percorso);
                break;
            }

            if (prefab == null && !mancanti.Contains(nome)) mancanti.Add(nome);
            cache[nome] = prefab;
            return prefab;
        }
    }
}
