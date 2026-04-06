using MyitCenter.TrayTicketTool.Models;

namespace MyitCenter.TrayTicketTool.Services;

public interface ITicketService
{
    Task<TicketResult> SubmitTicketAsync(Ticket ticket, byte[] screenshotPng);
}
