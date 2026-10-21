using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Строит примерку «как в игре» из НАСТОЯЩИХ стилей игры: элементу назначаются классы
    /// из ассета набора и подключается игровой StyleSheet — размеры/цвета/скругления берутся
    /// оттуда, поэтому мок обновляется вместе с игрой, а не разъезжается с ней.
    /// </summary>
    public static class WidgetPreviewBuilder
    {
        public static VisualElement Build(IconCollectionAsset col, Sprite sprite, IconItem item)
        {
            if (col == null || col.widgetUss == null || string.IsNullOrEmpty(col.widgetImageClass))
                return null;

            var host = new VisualElement();
            host.styleSheets.Add(col.widgetUss);
            host.style.flexDirection = FlexDirection.Row;
            host.style.marginTop = 4;

            var card = new VisualElement();
            if (!string.IsNullOrEmpty(col.widgetCardClass))
                card.AddToClassList(col.widgetCardClass);
            // Карточка в игре растягивается на ширину списка — в моке пусть будет по контенту
            card.style.alignSelf = Align.FlexStart;
            card.style.height = StyleKeyword.Auto;

            var image = new VisualElement();
            image.AddToClassList(col.widgetImageClass);
            image.style.backgroundImage = new StyleBackground(sprite);
            if (col.tintFrameByAccent && item != null)
            {
                image.style.borderLeftColor = item.Accent;
                image.style.borderRightColor = item.Accent;
                image.style.borderTopColor = item.Accent;
                image.style.borderBottomColor = item.Accent;
            }
            card.Add(image);

            if (!string.IsNullOrEmpty(col.widgetTitleClass) && item != null)
            {
                var titleLabel = new Label(item.DisplayName);
                titleLabel.AddToClassList(col.widgetTitleClass);
                card.Add(titleLabel);
            }

            host.Add(card);
            return host;
        }
    }
}
