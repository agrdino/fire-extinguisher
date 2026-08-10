using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Data
{
    public class FireExtinguisherConfigs : MonoBehaviour
    {
        [SerializeField] private List<FireExtinguisherConfig> _fireExtinguisherConfigs;
        
        private static FireExtinguisherConfigs _instance;
        public static FireExtinguisherConfigs Instance => _instance;

        private void Awake()
        {
            _instance = this;
        }

        public FireExtinguisherConfig GetFireExtinguisherConfig(int id)
        {
            return _fireExtinguisherConfigs.Find(x => x.id == id);
        }
    }
}