using Godot;

// Implemented by anything placed in a VerticalCarousel that wants to be sized from its artwork.
//
// The carousel used to detect this with `child is TextureRect`, which silently stopped working the
// moment entries gained a wrapper node -- every item would fall back to its existing height instead
// of the cover's aspect. Asking the item removes that coupling to the node type.
public interface ICarouselItem
{
    // Height / width of the artwork, or 0 when there is nothing to size from yet.
    float CoverAspectRatio { get; }
}
