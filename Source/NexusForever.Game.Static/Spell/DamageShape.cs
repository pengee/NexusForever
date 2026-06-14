namespace NexusForever.Game.Static.Spell
{
    public enum DamageShape
    {
        // param00 is radius. Full 2*pi span. Target-radius compensation can expand it through 14054da10.","param00=radius","telegraphDamageFlags bit 0x200 disables the target-radius compensation path."
        Circle        = 0,
        // param00 is inner or hole radius, clamped to >= 0; param01 is outer radius. Full 2*pi span.","param00=innerRadius; param01=outerRadius
        Ring          = 1,
        // Square / Box","param00, param01, and param02 are base extents converted to half-extents for rendering. Selected axes may include target-radius compensation through 14054da10.","param00=xExtent; param01=yExtent; param02=zExtent","NexusForever names this Square; client geometry is box-like.
        Square        = 2,
        // Creates two render payloads. The first follows shape 2; the second swaps the long/width axes, producing two perpendicular box arms.","param00=firstAxisExtent; param01=sharedAxisExtent; param02=secondAxisExtent","140549e10 creates a second payload when damageShapeEnum == 3.
        Cross         = 3,
        // Cone,"param00 is inner/start distance minus target radius and clamped to >= 0; param01 is outer/range radius; param02 is angle in degrees converted to radians and clamped to 2*pi.","param00=innerStartDistance; param01=outerRangeRadius; param02=angleDegrees"
        Cone          = 4,
        // Pie / Ring Sector","param00 is inner radius clamped to >= 0; param01 is outer radius; param02 is angle in degrees. The client stores span as 2*pi - angle and rotates orientation by pi.","param00=innerRadius; param01=outerRadius; param02=angleDegrees
        Pie           = 5,
        // Aura-like 3-Axis Volume","param00, param01, and param02 become a 3-axis scale/radius vector without the 0.5 half-extent conversion.","param00=xRadiusOrScale; param01=yRadiusOrScale; param02=zRadiusOrScale","Behavior is client-confirmed; no firmer client string name was found.
        Aura          = 6,
        // Rectangle,"Box-like extents converted to half-extents, with rectangle-specific origin/bounds handling. The client also writes a negative Z half-extent side value used later by 14054b1b0.","param00=xExtent; param01=yExtent; param02=zExtent
        Rectangle     = 7,
        // LongCone / Cone Frustum","param00 is inner/start distance minus target radius; param01 is outer/range radius; param02 is angle in degrees. param03, param04, and param05 are optional extra side/end extents; when nonzero, each is passed through 14054da10(..., 2.0) * 0.5, otherwise the outer radius fallback is used.","param00=innerStartDistance; param01=outerRangeRadius; param02=angleDegrees; param03=optionalExtentA; param04=optionalExtentB; param05=optionalExtentC","Optional param03..05 usage is client-confirmed, but authoring-side labels still need naming polish.
        LongCone      = 8
    }
}