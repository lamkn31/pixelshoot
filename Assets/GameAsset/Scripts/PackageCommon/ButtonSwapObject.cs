using UnityEngine.UI;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wayfu.Lamkn
{
    public class ButtonSwapObject : Button
    {
        [Header("Custom Button")]
        [SerializeField] private Transform objNormal;
        [SerializeField] private Transform objHighlighted;
        [SerializeField] private Transform objPressed;
        [SerializeField] private Transform objSelected;
        [SerializeField] private Transform objDisabled;

        protected override void Start()
        {
            base.Start();
        }
        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);

            switch (state)
            {
                case SelectionState.Normal:
                    SelectOneState(objNormal);
                    break;
                case SelectionState.Highlighted:
                    SelectOneState(objHighlighted);
                    break;
                case SelectionState.Pressed:
                    SelectOneState(objPressed);
                    break;
                case SelectionState.Selected:
                    SelectOneState(objSelected);
                    break;
                case SelectionState.Disabled:
                    SelectOneState(objDisabled);
                    break;
            }
        }

        private void SelectOneState(Transform obj)
        {
            objNormal.gameObject.SetActive(obj == objNormal);
            objHighlighted.gameObject.SetActive(obj == objHighlighted);
            objPressed.gameObject.SetActive(obj == objPressed);
            objSelected.gameObject.SetActive(obj == objSelected);
            objDisabled.gameObject.SetActive(obj == objDisabled);
        }

        #region Public State Control Methods

        /// <summary>
        /// Sets the button to Normal state (not active/selected)
        /// </summary>
        public void SetStateNormal()
        {
            DoStateTransition(SelectionState.Normal, false);
        }

        /// <summary>
        /// Sets the button to Selected state (active/selected)
        /// </summary>
        public void SetStateSelected()
        {
            DoStateTransition(SelectionState.Selected, false);
            
        }

        /// <summary>
        /// Sets the button to Disabled state
        /// </summary>
        public void SetStateDisabled()
        {
            DoStateTransition(SelectionState.Disabled, false);
        }

        /// <summary>
        /// Sets the button to Highlighted state (hover effect)
        /// </summary>
        public void SetStateHighlighted()
        {
            DoStateTransition(SelectionState.Highlighted, false);
        }

        /// <summary>
        /// Sets the button to Pressed state (click effect)
        /// </summary>
        public void SetStatePressed()
        {
            DoStateTransition(SelectionState.Pressed, false);
        }

        /// <summary>
        /// Checks if the button is currently in Selected state
        /// </summary>
        /// <returns>True if in Selected state</returns>
        public bool IsSelected()
        {
            return currentSelectionState == SelectionState.Selected;
        }

        /// <summary>
        /// Checks if the button is currently in Normal state
        /// </summary>
        /// <returns>True if in Normal state</returns>
        public bool IsNormal()
        {
            return currentSelectionState == SelectionState.Normal;
        }

        #endregion Public State Control Methods

    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ButtonSwapObject))]
    public class ButtonSwapObjectEditor : UnityEditor.Editor
    {
        ButtonSwapObject mtarget;
        private void OnEnable()
        {
            mtarget = target as ButtonSwapObject;
        }
    }
#endif
}