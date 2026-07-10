using UnityEngine;

namespace Wayfu.Lamkn
{
    public class AnimPopup : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject popup;

        private void OnValidate()
        {
            if (!animator)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (!popup)
            {
                popup = GetComponentInChildren<Animator>().gameObject;
            }
        }

        private void OnEnable()
        {
            if (popup != null && animator != null)
            {
                popup.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                animator.enabled = true;
            }
        }

        private void OnDisable()
        {
            if (animator != null)
            {
                animator.enabled = false;
            }
        }
    }
}