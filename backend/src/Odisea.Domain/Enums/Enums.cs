namespace Odisea.Domain.Enums;

public enum OwnerType { Operator, Agency }

public enum Visibility { PlatformShared, AgencyPrivate }

public enum BoardBasis { RoomOnly, BedAndBreakfast, HalfBoard, FullBoard, AllInclusive }

public enum Transport { Plane, Bus, Own }

public enum OfferStatus { Draft, Published }

public enum CollectionStatus { Draft, Published }

public enum ThemeStatus { Draft, Published }

public enum ExperienceStatus { Draft, Published }

public enum PublicationStatus { Draft, Published }

public enum UserStatus { Active, Suspended }

public enum TenantType { Agency, Operator }

// Hierarchical: PlatformAdmin > OperatorAdmin > AgencyAdmin > AgencyEditor
public enum UserRole { PlatformAdmin, OperatorAdmin, AgencyAdmin, AgencyEditor }
