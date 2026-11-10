using UnityEngine;

// Desliga o sol enquanto a câmara do jogador está dentro da cave e volta a ligá-lo quando sai.
// Assim o sol não "vaza" pelas juntas do teto da cave, e lá em baixo não se gastam as sombras do sol.
// Vai no objeto Volume_Cave: usa o Box Collider dele como zona.
// Não usa OnTriggerEnter/Exit de propósito: se o jogador morrer ou for teletransportado dentro da cave,
// o OnTriggerExit não chega a acontecer e o sol ficava desligado. Aqui confirma-se a posição em cada frame.
public class SolCave : MonoBehaviour
{
    [Tooltip("A luz do sol (o objeto Sun).")]
    public Light sol;

    [Tooltip("A câmara do jogador. Se ficar vazio, usa a câmara com a tag MainCamera.")]
    public Transform camaraJogador;

    [Tooltip("A caixa onde o sol fica desligado. Se ficar vazio, usa o Box Collider deste objeto.")]
    public BoxCollider zona;

    void Awake()
    {
        if (zona == null) zona = GetComponent<BoxCollider>();
    }

    void Update()
    {
        if (sol == null || zona == null) return;

        if (camaraJogador == null)
        {
            if (Camera.main == null) return;
            camaraJogador = Camera.main.transform;
        }

        bool dentro = zona.bounds.Contains(camaraJogador.position);

        // só mexe na luz quando o estado muda
        if (sol.enabled == dentro) sol.enabled = !dentro;
    }

    void OnDisable()
    {
        // se este script ou o objeto forem desligados, o sol nunca fica apagado
        if (sol != null) sol.enabled = true;
    }
}
