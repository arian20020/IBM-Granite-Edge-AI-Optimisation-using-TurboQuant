using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;

namespace GraniteEdgeAI.Features.ModelImport
{
    /// <summary>
    /// Displays a hand cursor only while the pointer is over
    /// the slider's draggable thumb.
    /// </summary>
    public sealed class ModelPreferenceSlider : Slider
    {
        // Creates one reusable Windows hand cursor.
        private static readonly InputCursor HandCursor =
            InputSystemCursor.Create(InputSystemCursorShape.Hand);

        // Stores the Thumb controls found inside the Slider template.
        private readonly List<Thumb> _templateThumbs = new();

        /// <summary>
        /// Runs when WinUI creates or recreates the Slider's visual template.
        /// </summary>
        protected override void OnApplyTemplate()
        {
            // Removes handlers from any previous template.
            RemoveThumbHandlers();

            // Allows the normal Slider template to be created.
            base.OnApplyTemplate();

            // Finds the draggable Thumb inside the generated template.
            FindThumbs(this, _templateThumbs);

            // Adds cursor behaviour to every Thumb found.
            foreach (Thumb thumb in _templateThumbs)
            {
                thumb.PointerEntered += Thumb_PointerEntered;
                thumb.PointerExited += Thumb_PointerExited;
                thumb.PointerCaptureLost += Thumb_PointerCaptureLost;
            }
        }

        // Changes the arrow into a hand when the thumb is hovered.
        private void Thumb_PointerEntered(
            object sender,
            PointerRoutedEventArgs e)
        {
            ProtectedCursor = HandCursor;
        }

        // Restores the normal cursor when the pointer leaves the thumb.
        private void Thumb_PointerExited(
            object sender,
            PointerRoutedEventArgs e)
        {
            ProtectedCursor = null;
        }

        // Also restores the cursor when the user finishes dragging.
        private void Thumb_PointerCaptureLost(
            object sender,
            PointerRoutedEventArgs e)
        {
            ProtectedCursor = null;
        }

        /// <summary>
        /// Recursively searches the generated visual tree for Thumb controls.
        /// </summary>
        private static void FindThumbs(
            DependencyObject parent,
            ICollection<Thumb> thumbs)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child =
                    VisualTreeHelper.GetChild(parent, index);

                if (child is Thumb thumb)
                {
                    thumbs.Add(thumb);
                }

                FindThumbs(child, thumbs);
            }
        }

        // Prevents event handlers from being duplicated if WinUI
        // recreates the control template.
        private void RemoveThumbHandlers()
        {
            foreach (Thumb thumb in _templateThumbs)
            {
                thumb.PointerEntered -= Thumb_PointerEntered;
                thumb.PointerExited -= Thumb_PointerExited;
                thumb.PointerCaptureLost -= Thumb_PointerCaptureLost;
            }

            _templateThumbs.Clear();
        }
    }
}