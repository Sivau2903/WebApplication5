//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace WebApplication5.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class CentralPurchaseOrder
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public CentralPurchaseOrder()
        {
            this.CentralPurchaseOrderItems = new HashSet<CentralPurchaseOrderItem>();
        }
    
        public int PONumber { get; set; }
        public Nullable<System.DateTime> PODate { get; set; }
        public string CentralDepartmentName { get; set; }
        public string CentralDepartmentAddress { get; set; }
        public string CentralDepartmentPhone { get; set; }
        public string CentralDepartmentEmail { get; set; }
        public string RequisitionNo { get; set; }
        public string ShipTo { get; set; }
        public string RequisitionedBy { get; set; }
        public string WhenShip { get; set; }
        public string ShipVia { get; set; }
        public string FOBPoint { get; set; }
        public string Terms { get; set; }
        public Nullable<int> CopiesOfInvoice { get; set; }
        public string AuthorizedBy { get; set; }
        public string CreatedBy { get; set; }
        public string Status { get; set; }
        public string StoreUploads { get; set; }
        public string PurchaseDepartmentUploads { get; set; }
        public string Statement { get; set; }
        public string SummarizerID { get; set; }
        public string CentralAccountantID { get; set; }
        public string IUCDApprovalDoc { get; set; }
        public Nullable<System.DateTime> IUCDDocUploadedDate { get; set; }
        public string IUCDDocUploadedBy { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<CentralPurchaseOrderItem> CentralPurchaseOrderItems { get; set; }
    }
}
