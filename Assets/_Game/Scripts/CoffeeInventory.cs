using System;
using UnityEngine;

namespace ColdBrewCase
{
    public sealed class CoffeeInventory : MonoBehaviour
    {
        public int capacity = 3;
        public int Count { get; private set; }
        public event Action Changed;
        public bool AddOne()
        {
            if (Count >= capacity) return false;
            Count++; Changed?.Invoke(); return true;
        }
        public bool Consume()
        {
            if (Count <= 0) return false;
            Count--; Changed?.Invoke(); return true;
        }
    }
}
