using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace _Scripts.Controller
{
    public class HandController : MonoBehaviour
    {
        #region ----- Component Config -----

        [SerializeField]
        private XRInputValueReader<float> _triggerInput = new XRInputValueReader<float>("Trigger");

        #endregion

        #region ----- Event ----

        public event Action<float> onPress; 

        #endregion
        
        private void OnEnable()
        {
            _triggerInput?.EnableDirectActionIfModeUsed();
        }

        private void Update()
        {
            onPress?.Invoke(_triggerInput.ReadValue());
        }

        private void OnDisable()
        {
            _triggerInput?.DisableDirectActionIfModeUsed();
        }
    }
}