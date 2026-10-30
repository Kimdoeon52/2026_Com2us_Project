namespace RealSteel.Dialogue
{
    public interface IUiStack
    {
        int Count { get; }
        void Push(object ui);
        void Remove(object ui);
    }

    public static class UiStack
    {
        public static IUiStack Current { get; set; }

        public static void Push(object ui)
        {
            Current?.Push(ui);
        }

        public static void Remove(object ui)
        {
            Current?.Remove(ui);
        }
    }
}
