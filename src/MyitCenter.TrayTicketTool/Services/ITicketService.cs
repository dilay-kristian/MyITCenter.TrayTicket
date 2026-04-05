using MyitCenter.TrayTicketTool.Models;

namespace MyitCenter.TrayTicketTool.Services;

public interface ITicketService
{
    Task<string> SubmitTicketAsync(Ticket ticket, byte[] screenshotPng);
}
