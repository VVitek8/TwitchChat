using UnityEngine;
using TMPro;

namespace PeakTextChat
{
    public class WaveMessageAnimation : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float amplitude = 5f;   // уменьшено с 8 до 5
        [SerializeField] private float speed = 2.5f;
        [SerializeField] private float delayBetweenLetters = 0.06f;

        private TextMeshProUGUI textComponent = null!;
        private TMP_TextInfo textInfo = null!;
        private Vector3[] originalPositions = null!;
        private Color originalColor;
        private float fontSize;

        public void Setup(float fontSize, Color color)
        {
            this.fontSize = fontSize;
            originalColor = color;
            textComponent = GetComponent<TextMeshProUGUI>();
            if (textComponent == null)
            {
                Debug.LogError("WaveMessageAnimation: TextMeshProUGUI component required!");
                return;
            }

            textComponent.ForceMeshUpdate();
            textInfo = textComponent.textInfo;
            originalPositions = new Vector3[textInfo.characterCount];

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (textInfo.characterInfo[i].isVisible)
                {
                    originalPositions[i] = textInfo.characterInfo[i].bottomLeft;
                }
            }
        }

        private void Update()
        {
            if (textComponent == null || textInfo == null) return;

            textComponent.ForceMeshUpdate();
            textInfo = textComponent.textInfo;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (!textInfo.characterInfo[i].isVisible) continue;

                int materialIndex = textInfo.characterInfo[i].materialReferenceIndex;
                int vertexIndex = textInfo.characterInfo[i].vertexIndex;

                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

                float delay = i * delayBetweenLetters;
                float offsetY = Mathf.Sin(Time.time * speed + delay) * amplitude;

                for (int j = 0; j < 4; j++)
                {
                    Vector3 pos = vertices[vertexIndex + j];
                    pos.y += offsetY;
                    vertices[vertexIndex + j] = pos;
                }
            }

            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
                textComponent.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
            }
        }

        private void OnDestroy()
        {
            if (textComponent != null)
            {
                textComponent.color = originalColor;
            }
        }
    }
}