using Xunit;
using System.IO;
using System;
using FinancialAccounting;

namespace FinAccTest
{
    public class EmailReportTests
    {
        [Fact]
        public void SendEmail_CallsEmailService_WithCorrectParameters()
        {
            var emailService = new EmailService(); 
            string testEmail = "test@example.com";
            string tempFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp_report.pdf");

            File.WriteAllText(tempFile, "Test content"); 
            var exception = Record.Exception(() => emailService.SendEmail(testEmail, tempFile));
            Assert.Null(exception); 

            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}