using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class SelectScene : MonoBehaviour
    {
        [SerializeField] private Button _btnSelect;
        [SerializeField] private List<FireExtinguisher> _fireExtinguishers;
    }
}