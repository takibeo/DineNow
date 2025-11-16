using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;

namespace DoAnChuyenNganh.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly string _fromEmail = "voky12460@gmail.com"; // Email của bạn
        private readonly string _appPassword = "gqwq pbzn xzkl kkbi"; // App Password Gmail

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            try
            {
                using var client = new SmtpClient("smtp.gmail.com", 587)
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(_fromEmail, _appPassword)
                };

                var mailMessage = new MailMessage(_fromEmail, email, subject, message)
                {
                    IsBodyHtml = true
                };

                await client.SendMailAsync(mailMessage);
            }
            catch
            {
                // Nếu email không tồn tại hoặc gửi thất bại → bỏ qua
            }
        }
    }
}
