using System;
using System.Linq.Expressions;
using MenuChanger;
using MenuChanger.MenuElements;
using Modding;
using UnityEngine;

namespace Archipelago.HollowKnight
{
    public static class ArchipelagoModeMenuConstructorHelpers
    {
        private readonly static Font _perpetua = CanvasUtil.GetFont("Perpetua");

        public static EntryField<T> CreateAndAddEntryField<T>(this MenuPage self, T settings, string name, string propertyName = null)
        {
            var newField = new EntryField<T>(self, name);
            newField.InputField.characterLimit = 500;
            var textRect = newField.InputField.gameObject.transform.Find("Text").GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(1500f, 63.2f);
            newField.InputField.textComponent.font = _perpetua;

            if (!string.IsNullOrEmpty(propertyName))
            {
                newField.Bind(settings, typeof(T).GetProperty(propertyName));
            }

            return newField;
        }

        public static EntryField<TSelection> CreateAndAddEntryField<T, TSelection>(this MenuPage self, Expression<Func<T, TSelection>> expr, T bindingObject = default)
        {
            Console.WriteLine($"{expr.Type.GenericTypeArguments[0]} x {expr.Type.GenericTypeArguments[1]}");

            return null;
        }
    }
}