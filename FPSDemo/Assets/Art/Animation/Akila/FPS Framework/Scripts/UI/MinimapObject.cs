using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Akila.FPSFramework
{
    public class MinimapObject : MonoBehaviour
    {
        [FormerlySerializedAs("iconSprite")]
        public Texture texture;
        public Color color = Color.white;
        public float orientation = 0f;
        public int order = -1; // Added ordering

        public Vector2 offset = Vector2.zero;
        public Vector2 size = Vector2.one;
        public bool rotateWithObject = false;


        public RectTransform Icon {  get; private set; }
        public RawImage IconImage { get; private set; }

        public bool visible { get; set; } = true;

        private void Start()
        {
            if(Minimap.Instance == null)
            {
                return;
            }

            Minimap.Instance.Register(this);

            GameObject iconGO = new GameObject($"{gameObject.name} [MinimapObject]", typeof(RectTransform), typeof(RawImage));
            Icon = iconGO.GetComponent<RectTransform>();
            IconImage = iconGO.GetComponent<RawImage>();

            Icon.SetParent(Minimap.Instance.iconsContainer, false);

            if (texture)
                IconImage.texture = texture;

            IconImage.color = color;
            Icon.sizeDelta = size;
        }

        private void Update()
        {
            if(Icon == null || IconImage == null)
                return;

            IconImage.enabled = visible;
        }

        public void Show() => visible = true;
        public void Hide() => visible = false;

        private void OnDisable()
        {
            if (Minimap.Instance != null)
                Minimap.Instance.Unregister(this);

            if (Icon != null)
                Destroy(Icon.gameObject);
        }
    }
}
