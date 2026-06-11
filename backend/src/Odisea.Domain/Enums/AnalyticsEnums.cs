namespace Odisea.Domain.Enums;

// Analytics event taxonomy. Kept in a dedicated file (not Enums.cs) to avoid
// merge conflicts with the parallel Experience/embed-security branches.
public enum EventType { Impression, Open, InquiryStart, InquirySubmit }

public enum Channel { WebComponent, Iframe, WordPress, Api }
