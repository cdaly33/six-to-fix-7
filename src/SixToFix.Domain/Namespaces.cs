// Namespace anchors — establishes SixToFix.Domain and SixToFix.Domain.Entities
// so that global using directives in Application and Infrastructure compile.
// Neo will add entity classes to SixToFix.Domain.Entities during Phase 1.

namespace SixToFix.Domain
{
    /// <summary>Marker interface. Neo will add domain interfaces alongside this.</summary>
    public interface IDomainMarker { }
}

namespace SixToFix.Domain.Entities
{
    /// <summary>Marker interface. Neo will add entity classes alongside this.</summary>
    public interface IEntityMarker { }
}
