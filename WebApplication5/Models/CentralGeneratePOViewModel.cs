using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication5.Models
{
    public class CentralGeneratePOViewModel
    {
        internal List<CentralPurchaseOrderItem> centralpurchaseOrderItems;

        public string PONumber { get; set; }
        public string VendorEmail { get; set; }
        public string RequisitionNo { get; set; }
        public string CentralDepartmentName { get; set; }
        public string CentralDepartmentPhone { get; set; }
        public string CentralDepartmentEmail { get; set; }
        public string CentralDepartmentAddress { get; set; }
        public DateTime PODate { get; set; }

        public string ShipTo { get; set; }
        public string RequisitionedBy { get; set; }
        public string WhenShip { get; set; }
        public string ShipVia { get; set; }
        public string FOBPoint { get; set; }
        public string Terms { get; set; }
        public string AuthorizedBy { get; set; }
        public string Status { get; set; }
        public int? CopiesOfInvoice { get; set; }
        //public int InvoiceCopies { get; set; }

        public HttpPostedFileBase CertificationFile { get; set; }
        public string CertificationFileName { get; set; }

        public string StoreUploads {get; set;}
        public string Statement { get; set; }
        public string CertificationFilePath { get; set; }


        public string PurchaseDepartmentUploads { get; set; }
        public List<CentralPurchaseOrderItem> CentralPurchaseOrderItems { get; set; }
        public List<CentralPurchaseOrder> CentralPurchaseOrder { get; set; }

        //public static implicit operator GeneratePOViewModel(GeneratePOViewModel v)
        //{
        //    throw new NotImplementedException();
        //}
    }
}