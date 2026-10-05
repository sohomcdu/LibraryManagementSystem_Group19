namespace LibraHub.Pages.Account;
/// <summary>Demo stub — spec does not implement real email delivery, so this just confirms the flow.</summary>
public class ForgotModel : AppPageModel { public bool Sent { get; set; } public void OnPost() => Sent = true; }
