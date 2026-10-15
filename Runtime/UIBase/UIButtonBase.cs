using UnityEngine;
using System;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;

namespace Zeraveene.Shared
{
    // Ensure RectTransform exists
    [RequireComponent(typeof(RectTransform))]
    [MovedFrom(true, sourceNamespace: "MaxsusWorks.Shared", sourceAssembly: "MaxsusWorks.ProceduralUI")]
    public class UIButtonBase : UIBase
    {
        #region Red Dot - Inspector
        [Header("Red Dot")]
        [SerializeField] private Transform _redDot;
        #endregion

        #region Runtime Data
        private Button _button;
        #endregion

        #region Properties
        public Button Button => _button;
        public Button.ButtonClickedEvent onClick => _button.onClick;
        #endregion

        #region Public Methods
        protected override void HandleInitialize()
        {
            _button = GetComponent<Button>();
            SetReddot(false);
            base.HandleInitialize();
        }

        public void SetReddot(bool show)
        {
            if (_redDot == null) return;
            _redDot.gameObject.SetActive(show);
        }
        #endregion
    }
}
