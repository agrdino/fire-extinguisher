using UnityEngine;

namespace _Scripts.Controller
{
    public abstract class HandRayInteractable : MonoBehaviour
    {
        public abstract bool IsInteractionEnabled { get; }

        public abstract void SetHovered(bool isHovered);
        public abstract bool TryActivate();
    }
}
