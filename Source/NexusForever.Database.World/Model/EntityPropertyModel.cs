namespace NexusForever.Database.World.Model
{
    public class EntityPropertyModel
    {
        public uint Id { get; set; }
        public uint Property { get; set; }
        public uint Value { get; set; }

        public EntityModel Entity { get; set; }
    }
}
