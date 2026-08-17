using PermitToWork.Domain.Common;

namespace PermitToWork.Domain.Organization;

// The physical hierarchy, three levels deep:
//
//   Facility        a site on the map              "Refinery North"
//     └─ Building   a unit or area within it       "Distillation Unit 3"
//          └─ Location   a specific space          "Room 2.14", "East garage"
//
// A permit points at a Location and therefore knows its Building and Facility
// transitively. Copying those onto the permit as well would create two sources of truth
// that can disagree after a reorganisation.

/// <summary>A site — a place on the map, with its own gate and its own address.</summary>
public class Facility : Entity
{
    private Facility() { }

    public Facility(string code, string name, string? description = null)
    {
        Code = Guard.Required(code, "Facility code", 20).ToUpperInvariant();
        Name = Guard.Required(name, "Facility name", 200);
        Description = Guard.Optional(description, "Description", 500);
    }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// The name and description can change; the code cannot. Codes appear on permits, on
    /// team codes and on badge numbers already issued — renaming one would silently
    /// rewrite what those identifiers meant.
    /// </summary>
    public void Rename(string name, string? description)
    {
        Name = Guard.Required(name, "Facility name", 200);
        Description = Guard.Optional(description, "Description", 500);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}

/// <summary>A unit or area inside a facility. A facility has many.</summary>
public class Building : Entity
{
    private Building() { }

    public Building(Guid facilityId, string code, string name, string? description = null)
    {
        FacilityId = Guard.Required(facilityId, "Facility");
        Code = Guard.Required(code, "Building code", 20).ToUpperInvariant();
        Name = Guard.Required(name, "Building name", 200);
        Description = Guard.Optional(description, "Description", 500);
    }

    public Guid FacilityId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Rename(string name, string? description)
    {
        Name = Guard.Required(name, "Building name", 200);
        Description = Guard.Optional(description, "Description", 500);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}

/// <summary>
/// A specific space inside a building — a room, an office, a garage, a closet.
/// The finest granularity work is located at.
/// </summary>
public class Location : Entity
{
    private Location() { }

    public Location(Guid buildingId, string code, string name, string? description = null)
    {
        BuildingId = Guard.Required(buildingId, "Building");
        Code = Guard.Required(code, "Location code", 20).ToUpperInvariant();
        Name = Guard.Required(name, "Location name", 200);
        Description = Guard.Optional(description, "Description", 500);
    }

    public Guid BuildingId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Rename(string name, string? description)
    {
        Name = Guard.Required(name, "Location name", 200);
        Description = Guard.Optional(description, "Description", 500);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
