using UnityEngine;

namespace Valdorso.World
{
    /// <summary>
    /// Fa girare piano il cielo HDRI, così le nuvole scorrono.
    /// Solo estetica: ognuno la vede sul suo PC, il server non c'entra.
    /// Lavora su una copia del materiale del cielo, così il file del progetto non cambia mai.
    /// Primo passo: più avanti le nuvole avranno un loro strato che si muove col vento e col giorno e la notte.
    /// </summary>
    public class NuvoleInMovimento : MonoBehaviour
    {
        [Tooltip("Gradi al secondo: 0,1 = un giro intero in un'ora")]
        public float velocita = 0.1f;

        Material cielo;
        float partenza;

        void Start()
        {
            if (RenderSettings.skybox == null || !RenderSettings.skybox.HasProperty("_Rotation"))
            {
                enabled = false;
                return;
            }
            cielo = new Material(RenderSettings.skybox);
            RenderSettings.skybox = cielo;
            partenza = cielo.GetFloat("_Rotation");
        }

        void Update()
        {
            cielo.SetFloat("_Rotation", Mathf.Repeat(partenza + velocita * Time.time, 360f));
        }

        void OnDestroy()
        {
            if (cielo != null) Destroy(cielo);
        }
    }
}
