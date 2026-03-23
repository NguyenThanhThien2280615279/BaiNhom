using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EcommerceSecondHand.Models;
using Microsoft.AspNetCore.Http;

namespace EcommerceSecondHand.Repositories.Interfaces
{
    public interface  IVnPayService
    {
        string CreatePaymentUrl(PaymentInformationModel model, HttpContext context);
        PaymentResponseModel PaymentExecute(IQueryCollection collections);
    }
}