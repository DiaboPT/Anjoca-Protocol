using UnityEditor;
using UnityEngine;

// Inspector do Pickup: mostra só os campos que interessam ao tipo escolhido.
// Sem isto, o Heal Amount aparecia mesmo com o Type em Ammo Pack, e as Balas num pack de vida.
// TEM de ficar numa pasta chamada Editor (por exemplo Assets/ANJOCA/Editor), senão a build falha.
// Não muda nada no jogo: é só o aspeto do Inspector.
[CustomEditor(typeof(Pickup))]
[CanEditMultipleObjects]
public class PickupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // o campo "Script" a cinzento, como em qualquer componente
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        SerializedProperty tipo = serializedObject.FindProperty("type");
        EditorGUILayout.PropertyField(tipo);

        // com vários objetos selecionados de tipos diferentes, mostra tudo
        bool varios = tipo.hasMultipleDifferentValues;
        Pickup.PickupType escolhido = (Pickup.PickupType)tipo.enumValueIndex;

        if (varios || escolhido == Pickup.PickupType.HealthPack)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Vida", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("healAmount"));
        }

        if (varios || escolhido == Pickup.PickupType.AmmoPack)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Munição", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("balas"));
        }

        // os módulos (salto, blindagem, precisão, visor térmico e velocidade) não têm campos
        // próprios: só desbloqueiam o aprimoramento no SuitAbilities

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Aspeto e som (opcional)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("velocidadeRodar"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("somApanhar"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("duracao"));

        serializedObject.ApplyModifiedProperties();
    }
}
