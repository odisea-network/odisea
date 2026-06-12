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

// How a supplier's offers reach our import pipeline.
public enum SupplierConnectionKind { Manual, Xml, JsonApi, CsvSftp }

public enum SupplierConnectionStatus { Active, Paused, Failed }

// Lifecycle of a single offer as seen from its source connection.
public enum ImportState { Pending, Imported, Stale, Failed }

// Lifecycle of a single connector run.
public enum ImportJobStatus { Running, Succeeded, Failed }

// Commercial relationship between an operator and an agency.
public enum EntitlementStatus { Active, Suspended }

// A general expression of interest vs. a structured request against a specific offer.
public enum LeadKind { Inquiry, BookingRequest }

// Inbox pipeline for a lead.
public enum LeadStatus { New, Contacted, Converted, Closed }
