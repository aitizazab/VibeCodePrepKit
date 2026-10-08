using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.Shell
{
    /// <summary>A dialogue character: a base sprite plus named expression layers drawn on top.</summary>
    [CreateAssetMenu(menuName = "JamKit/Portrait", fileName = "Portrait")]
    public sealed class Portrait : ScriptableObject
    {
        [Serializable]
        public class Expression
        {
            public string name = "happy";
            [Tooltip("Drawn over the base sprite, same size (transparent where the base shows through).")]
            public Sprite sprite;
        }

        [Tooltip("The id used in ink: #portrait:Id expression left")]
        public string characterId = "Guide";
        public Sprite baseSprite;
        [Tooltip("Used when ink names no expression (#portrait:Id left).")]
        public string defaultExpression = "neutral";
        public List<Expression> expressions = new List<Expression>();

        public Sprite FindExpression(string expressionName)
        {
            if (string.IsNullOrEmpty(expressionName)) expressionName = defaultExpression;
            if (expressions == null || string.IsNullOrEmpty(expressionName)) return null;
            foreach (var e in expressions)
                if (e != null && string.Equals(e.name, expressionName, StringComparison.OrdinalIgnoreCase)) return e.sprite;
            return null;
        }
    }
}
