namespace Melodica.Services.Media;

public delegate PlayableMedia MediaGetter();

public record LazyMedia
{
    public LazyMedia(MediaGetter getter)
    {
        this.getter = getter;
    }

    public LazyMedia(PlayableMedia media)
    {
        getter = () => media;
    }

    readonly MediaGetter getter;
    PlayableMedia? cache;

    public static implicit operator LazyMedia(PlayableMedia media)
    {
        return new(media);
    }

    public static implicit operator LazyMedia(MediaGetter getter)
    {
        return new(getter);
    }

    public static implicit operator PlayableMedia(LazyMedia media)
    {
        return media.cache ??= media.getter();
    }
}
