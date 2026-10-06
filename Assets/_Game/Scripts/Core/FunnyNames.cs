namespace LightsOut
{
    /// <summary>Random default names, so nobody is called "Player 3".</summary>
    public static class FunnyNames
    {
        static readonly string[] First =
        {
            "Sir", "Captain", "Lil", "Big", "Sneaky", "Sleepy", "Angry", "Soggy", "Wobbly", "Doctor", "Professor",
            "Grandma", "Turbo", "Spicy", "Moist", "Tiny",
        };

        static readonly string[] Last =
        {
            "Bonks", "Noodle", "Potato", "Pickle", "Toaster", "Nugget", "Goblin", "Waffle", "Biscuit", "Burrito",
            "Sock", "Pudding", "Meatball", "Llama", "Stapler", "Samosa",
        };

        public static string Random() =>
            First[UnityEngine.Random.Range(0, First.Length)] + " " + Last[UnityEngine.Random.Range(0, Last.Length)];
    }
}
