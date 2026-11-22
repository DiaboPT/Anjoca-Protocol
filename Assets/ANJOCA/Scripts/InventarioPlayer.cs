using UnityEngine;

// Fica no Player. Guarda os objetos especiais que o jogador já apanhou.
public class InventarioPlayer : MonoBehaviour
{
    // passa a verdadeiro quando o jogador apanha o cartão da sala técnica
    public bool temCartao = false;

    
    // cargas explosivas que o jogador traz (CargaExplosiva soma, PortaExplosivos gasta uma)
    public int cargas = 0;
}