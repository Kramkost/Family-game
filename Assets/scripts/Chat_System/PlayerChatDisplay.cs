using System.Collections;
using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;
using TMPro;
// =========================================================================
    // 2. ВЬЮВЕР ЧАТА (Game Feel, Vertex Animation, Audio)
    // =========================================================================
    [RequireComponent(typeof(PlayerChatNetwork))]
    public class PlayerChatDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text chatText;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private Transform bubbleTransform;

        [Header("Game Feel : Audio")]
        [SerializeField] private AudioClip[] popupSounds;

        [Header("Game Feel : Animation")]
        [SerializeField] private float displayDuration = 4f;
        [SerializeField] private float scaleSmoothTime = 0.15f;
        [SerializeField] private float waveSpeed = 5f;
        [SerializeField] private float waveHeight = 0.05f;
        [SerializeField] private float waveSpacing = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool showDebug;

        private PlayerChatNetwork _network;
        private Coroutine _hideCoroutine;
        
        private Vector3 _currentVelocity;
        private float _targetScale = 0f;
        private bool _isDisplaying;
        
        private TMP_MeshInfo[] _cachedMeshInfo;

        private void Awake()
        {
            _network = GetComponent<PlayerChatNetwork>();
            bubbleTransform.localScale = Vector3.zero;
            chatText.text = "";
        }

        private void OnEnable() => _network.OnMessageReceived += HandleNewMessage;
        private void OnDisable() => _network.OnMessageReceived -= HandleNewMessage;

        private void HandleNewMessage(string message)
        {
            chatText.text = message;
            chatText.ForceMeshUpdate();
            _cachedMeshInfo = chatText.textInfo.CopyMeshInfoVertexData();

            PlayRandomSound(popupSounds);
            ShowBubble();

            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            _hideCoroutine = StartCoroutine(HideRoutine());
        }

        private void ShowBubble()
        {
            _targetScale = 1f;
            _isDisplaying = true;
        }

        private IEnumerator HideRoutine()
        {
            yield return new WaitForSeconds(displayDuration);
            _targetScale = 0f;
            _isDisplaying = false;
        }

        private void Update()
        {
            bubbleTransform.localScale = Vector3.SmoothDamp(
                bubbleTransform.localScale, 
                Vector3.one * _targetScale, 
                ref _currentVelocity, 
                scaleSmoothTime
            );

            if (_isDisplaying && bubbleTransform.localScale.sqrMagnitude > 0.01f)
            {
                AnimateTextVertices();
            }
        }

        private void AnimateTextVertices()
        {
            chatText.ForceMeshUpdate();
            TMP_TextInfo textInfo = chatText.textInfo;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;

                Vector3[] sourceVertices = _cachedMeshInfo[materialIndex].vertices;
                Vector3[] destinationVertices = textInfo.meshInfo[materialIndex].vertices;

                float offset = Mathf.Sin(Time.time * waveSpeed + i * waveSpacing) * waveHeight;

                for (int v = 0; v < 4; v++)
                {
                    destinationVertices[vertexIndex + v] = sourceVertices[vertexIndex + v] + new Vector3(0, offset, 0);
                }
            }

            chatText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        private void PlayRandomSound(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0 || audioSource == null) return;
            audioSource.clip = clips[Random.Range(0, clips.Length)];
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.Play();
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebug || bubbleTransform == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(bubbleTransform.position, 0.5f);
            Gizmos.DrawRay(bubbleTransform.position, Vector3.up * 1.5f);
        }
    }