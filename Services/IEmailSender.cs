using System.Threading.Tasks;

namespace DoAnChuyenNganh.Services
{
    public interface IEmailSender
    {
        /// <summary>
        /// Gửi email
        /// </summary>
        /// <param name="email">Email nhận</param>
        /// <param name="subject">Tiêu đề</param>
        /// <param name="message">Nội dung (HTML)</param>
        Task SendEmailAsync(string email, string subject, string message);
    }
}
