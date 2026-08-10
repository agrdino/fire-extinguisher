using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Data
{
    [CreateAssetMenu(fileName = "FireExtinguisherConfigs", menuName = "FireExtinguisherConfigs")]
    public class FireExtinguisherConfigs : ScriptableObject
    {
        [SerializeField] private List<FireExtinguisherConfig> _fireExtinguisherConfigs;

        public FireExtinguisherConfig GetFireExtinguisherConfig(int id)
        {
            return _fireExtinguisherConfigs.Find(x => x.id == id);
        }
    }
}