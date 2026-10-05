namespace LibraHub.Domain;

public enum Role { Patron, Reception, Manager, Admin, Kiosk }

/// <summary>Spec section 4 – cross-feature item status lifecycle.</summary>
public enum ItemStatus { Available, Borrowed, Damaged, Reserved, InTransit }

/// <summary>Spec F5 – Reservation states.</summary>
public enum HoldStatus { Waiting, Ready, Fulfilled, Cancelled, Expired }

/// <summary>Spec F4 – Requested › Dispatched › Received (or Cancelled).</summary>
public enum TransferStatus { Requested, Dispatched, Received, Cancelled }

/// <summary>Spec F3 – notification types.</summary>
public enum NotificationType { Borrow, DueSoon, Overdue, HoldAvailable, Transfer }

public enum Channel { Email, Sms }

public enum SendStatus { SimulatedSent, SimulatedFailed }

public enum FineStatus { Outstanding, Collected, Waived }

[Flags]
public enum Channels { None = 0, Email = 1, Sms = 2 }
