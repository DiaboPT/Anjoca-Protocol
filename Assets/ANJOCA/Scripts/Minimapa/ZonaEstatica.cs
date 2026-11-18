using UnityEngine;

// Caixa invisível do tamanho do edifício: enquanto o Player estiver lá dentro, o minimapa fica em estática.
// Precisa de um ou mais Box Collider com Is Trigger ligado no mesmo objeto. Layer: Ignore Raycast.
public class ZonaEstatica : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        // só reage ao Player (é o único com PlayerStats)
        if (other.GetComponent<PlayerStats>() == null) return;
        if (EstaticaMinimapa.Instancia != null) EstaticaMinimapa.Instancia.Entrar();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerStats>() == null) return;
        if (EstaticaMinimapa.Instancia != null) EstaticaMinimapa.Instancia.Sair();
    }
}
