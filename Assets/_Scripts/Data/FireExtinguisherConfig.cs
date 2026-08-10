using UnityEngine;

namespace _Scripts.Data
{
    [CreateAssetMenu(fileName = "FireExtinguisherConfig", menuName = "FireExtinguisherConfig")]
    public class FireExtinguisherConfig : ScriptableObject
    {
        public int id;
        public new string name;
        public string description;
        public Sprite icon;
    }
}