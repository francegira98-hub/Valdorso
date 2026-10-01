using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Valdorso → Prove → Diagnosi dello sfarfallio: da lanciare IN PLAY, con il personaggio nella valle.
    /// Dice quale telecamera disegna davvero il gioco e con quale anti-aliasing, come sono le ombre,
    /// quando gli alberi cambiano dettaglio (LOD e billboard) e com'è il vento. Copia tutto negli appunti.
    /// Solo editor.
    /// </summary>
    public static class DiagnosiSfarfallio
    {
        [MenuItem("Valdorso/Prove/Diagnosi dello sfarfallio (in Play)")]
        static void Diagnosi()
        {
            var ci = CultureInfo.InvariantCulture;
            var t = new StringBuilder();
            t.AppendLine("DIAGNOSI DELLO SFARFALLIO - " + (EditorApplication.isPlaying ? "in Play" : "GIOCO FERMO (va lanciata in Play, nella valle)"));
            t.AppendLine("fps circa " + (1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("0", ci)
                         + ", vsync " + QualitySettings.vSyncCount + ", fps massimi " + Application.targetFrameRate);

            Camera principale = Camera.main;
            t.AppendLine("Telecamera principale: " + (principale != null ? principale.name + " (scena " + principale.gameObject.scene.name + ")" : "nessuna"));
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var d = c.GetUniversalAdditionalCameraData();
                t.AppendLine("- " + c.name + ": scena " + c.gameObject.scene.name + ", accesa " + c.isActiveAndEnabled
                             + ", profondita " + c.depth.ToString(ci) + ", HDR " + c.allowHDR
                             + (d != null
                                 ? ", tipo " + d.renderType + ", anti-aliasing " + d.antialiasing + " " + d.antialiasingQuality
                                   + ", post-processing " + d.renderPostProcessing
                                 : ", senza dati URP")
                             + ", vicino " + c.nearClipPlane.ToString(ci) + ", lontano " + c.farClipPlane.ToString(ci));
            }

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset u)
                t.AppendLine("URP " + u.name + ": scala " + u.renderScale.ToString(ci) + ", filtro " + u.upscalingFilter
                             + ", MSAA " + u.msaaSampleCount + ", cascate delle ombre " + u.shadowCascadeCount
                             + ", ombre fino a " + u.shadowDistance.ToString(ci) + " m, ombre morbide " + u.supportsSoftShadows);
            t.AppendLine("LOD bias " + QualitySettings.lodBias.ToString(ci) + ", LOD massimo " + QualitySettings.maximumLODLevel
                         + ", filtro anisotropico " + QualitySettings.anisotropicFiltering);

            Terrain conAlberi = null;
            foreach (Terrain tr in Terrain.activeTerrains)
                if (tr.terrainData.treeInstanceCount > 0) { conAlberi = tr; break; }
            if (conAlberi != null)
            {
                t.AppendLine("Terreno " + conAlberi.name + ": alberi fino a " + conAlberi.treeDistance.ToString(ci)
                             + " m, billboard da " + conAlberi.treeBillboardDistance.ToString(ci)
                             + " m, dissolvenza " + conAlberi.treeCrossFadeLength.ToString(ci)
                             + ", alberi pieni al massimo " + conAlberi.treeMaximumFullLODCount
                             + ", erba fino a " + conAlberi.detailObjectDistance.ToString(ci) + " m");
                TreePrototype[] alberi = conAlberi.terrainData.treePrototypes;
                for (int i = 0; i < alberi.Length && i < 8; i++)
                {
                    GameObject p = alberi[i].prefab;
                    if (p == null) continue;
                    var lod = p.GetComponent<LODGroup>();
                    string testo = "  albero " + p.name + ": ";
                    if (lod == null) testo += "senza LODGroup";
                    else
                    {
                        LOD[] livelli = lod.GetLODs();
                        testo += livelli.Length + " livelli, passaggio " + lod.fadeMode + ", dissolvenza animata " + lod.animateCrossFading + ", soglie";
                        foreach (LOD l in livelli) testo += " " + l.screenRelativeTransitionHeight.ToString("0.###", ci);
                    }
                    var r = p.GetComponentInChildren<Renderer>();
                    if (r != null && r.sharedMaterial != null) testo += ", shader " + r.sharedMaterial.shader.name;
                    t.AppendLine(testo);
                }
            }

            foreach (WindZone w in Object.FindObjectsByType<WindZone>(FindObjectsSortMode.None))
                t.AppendLine("Vento " + w.name + ": " + w.mode + ", forza " + w.windMain.ToString(ci) + ", turbolenza " + w.windTurbulence.ToString(ci)
                             + ", raffiche " + w.windPulseMagnitude.ToString(ci) + " ogni " + w.windPulseFrequency.ToString(ci));

            EditorGUIUtility.systemCopyBuffer = t.ToString();
            Debug.Log("[Valdorso] Diagnosi dello sfarfallio copiata negli appunti.");
            EditorUtility.DisplayDialog("Valdorso", "Diagnosi dello sfarfallio copiata negli appunti: incollala nella chat (Ctrl+V).", "OK");
        }
        /// <summary>
        /// Valdorso → Prove → Diagnosi dei materiali dell'erba (30/09): per le macchie bianche nell'erba, che non cambiano
        /// con l'esposizione. Per ogni tipo d'erba e di fiore elenca il materiale, le sue texture (VUOTA = manca) e i suoi numeri.
        /// </summary>
        [MenuItem("Valdorso/Prove/Diagnosi dei materiali dell'erba")]
        static void Materiali()
        {
            var ci = CultureInfo.InvariantCulture;
            var t = new StringBuilder();
            t.AppendLine("MATERIALI DELL'ERBA E DEI FIORI");
            Terrain tr = null;
            foreach (Terrain x in Terrain.activeTerrains) if (x.terrainData.detailPrototypes.Length > 0) { tr = x; break; }
            if (tr == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Nessuna tessera con l'erba: apri la scena Valle.", "OK");
                return;
            }
            var visti = new HashSet<Material>();
            DetailPrototype[] proto = tr.terrainData.detailPrototypes;
            for (int k = 0; k < proto.Length; k++)
            {
                GameObject p = proto[k].prototype;
                if (p == null) continue;
                foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null) { t.AppendLine(k + ". " + p.name + ": MATERIALE MANCANTE"); continue; }
                        if (!visti.Add(m)) { t.AppendLine(k + ". " + p.name + ": " + m.name + " (vedi sopra)"); continue; }
                        t.AppendLine(k + ". " + p.name + ": materiale " + m.name + ", shader " + m.shader.name
                                     + ", coda " + m.renderQueue + ", parole chiave: " + string.Join(" ", m.shaderKeywords));
                        foreach (string nome in m.GetTexturePropertyNames())
                        {
                            if (!m.HasProperty(nome)) continue;
                            Texture tex = m.GetTexture(nome);
                            t.AppendLine("     " + nome + ": " + (tex != null ? tex.name + " " + tex.width + "x" + tex.height : "VUOTA"));
                        }
                        var numeri = new List<string>();
                        int n = m.shader.GetPropertyCount();
                        for (int i = 0; i < n; i++)
                        {
                            string nome = m.shader.GetPropertyName(i);
                            switch (m.shader.GetPropertyType(i))
                            {
                                case UnityEngine.Rendering.ShaderPropertyType.Color:
                                    Color c = m.GetColor(nome);
                                    numeri.Add(nome + "=(" + c.r.ToString("0.##", ci) + "," + c.g.ToString("0.##", ci) + "," + c.b.ToString("0.##", ci) + "," + c.a.ToString("0.##", ci) + ")");
                                    break;
                                case UnityEngine.Rendering.ShaderPropertyType.Float:
                                case UnityEngine.Rendering.ShaderPropertyType.Range:
                                    numeri.Add(nome + "=" + m.GetFloat(nome).ToString("0.###", ci));
                                    break;
                            }
                        }
                        t.AppendLine("     " + string.Join(", ", numeri));
                    }
                }
            }
            EditorGUIUtility.systemCopyBuffer = t.ToString();
            Debug.Log("[Valdorso] Diagnosi dei materiali dell'erba copiata negli appunti.");
            EditorUtility.DisplayDialog("Valdorso", "Diagnosi dei materiali dell'erba copiata negli appunti: incollala nella chat (Ctrl+V).", "OK");
        }
        // ---------- Prova: nascondere un tipo di pianta per capire se è lui a fare le macchie bianche ----------
        // Si nasconde portando la sua altezza a zero; le altezze vere restano salvate nell'editor e si rimettono uguali.
        const string ChiaveNascosti = "Valdorso.ProvaErba.Nascosti";

        [MenuItem("Valdorso/Prove/Erba: nascondi la camomilla (prova)")]
        static void NascondiCamomilla() => Nascondi("chamomile");

        [MenuItem("Valdorso/Prove/Erba: rimetti le piante nascoste")]
        static void Rimetti()
        {
            string salvato = EditorPrefs.GetString(ChiaveNascosti, "");
            if (salvato == "") { Debug.Log("[Valdorso] Nessuna pianta nascosta."); return; }
            // formato: indice:min:max;indice:min:max...
            foreach (Terrain tr in Terrain.activeTerrains)
            {
                DetailPrototype[] proto = tr.terrainData.detailPrototypes;
                if (proto.Length == 0) continue;
                foreach (string pezzo in salvato.Split(';'))
                {
                    string[] v = pezzo.Split(':');
                    if (v.Length != 3) continue;
                    int k = int.Parse(v[0], CultureInfo.InvariantCulture);
                    if (k >= proto.Length) continue;
                    proto[k].minHeight = float.Parse(v[1], CultureInfo.InvariantCulture);
                    proto[k].maxHeight = float.Parse(v[2], CultureInfo.InvariantCulture);
                }
                tr.terrainData.detailPrototypes = proto;
                EditorUtility.SetDirty(tr.terrainData);
            }
            EditorPrefs.DeleteKey(ChiaveNascosti);
            AssetDatabase.SaveAssets();
            Debug.Log("[Valdorso] Piante nascoste rimesse com'erano.");
        }

        static void Nascondi(string parteDelNome)
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Valdorso", "Ferma prima il Play.", "OK"); return; }
            if (EditorPrefs.GetString(ChiaveNascosti, "") != "")
            {
                EditorUtility.DisplayDialog("Valdorso", "C'è già una pianta nascosta: prima Valdorso → Prove → Erba: rimetti le piante nascoste.", "OK");
                return;
            }
            var ci = CultureInfo.InvariantCulture;
            var salvati = new List<string>();
            var nomi = new List<string>();
            bool primo = true;
            foreach (Terrain tr in Terrain.activeTerrains)
            {
                DetailPrototype[] proto = tr.terrainData.detailPrototypes;
                if (proto.Length == 0) continue;
                for (int k = 0; k < proto.Length; k++)
                {
                    string nome = proto[k].prototype != null ? proto[k].prototype.name : "";
                    if (!nome.ToLowerInvariant().Contains(parteDelNome)) continue;
                    if (primo)
                    {
                        salvati.Add(k + ":" + proto[k].minHeight.ToString(ci) + ":" + proto[k].maxHeight.ToString(ci));
                        nomi.Add(k + ". " + nome);
                    }
                    proto[k].minHeight = 0.001f;
                    proto[k].maxHeight = 0.001f;
                }
                primo = false;
                tr.terrainData.detailPrototypes = proto;
                EditorUtility.SetDirty(tr.terrainData);
            }
            EditorPrefs.SetString(ChiaveNascosti, string.Join(";", salvati));
            AssetDatabase.SaveAssets();
            Debug.Log("[Valdorso] Nascosti per prova: " + (nomi.Count > 0 ? string.Join(", ", nomi) : "nessun tipo trovato") + ". Per rimetterli: Valdorso → Prove → Erba: rimetti le piante nascoste.");
        }
    }
}