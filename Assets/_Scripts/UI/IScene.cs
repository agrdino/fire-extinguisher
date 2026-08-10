using UnityEngine;

namespace _Scripts.UI
{
    public interface IScene
    {
        public GameObject gameObject { get; }
        public void Show();
        public void Hide();
    }
}