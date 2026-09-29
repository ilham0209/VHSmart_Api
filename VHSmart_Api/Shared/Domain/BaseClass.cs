using System.ComponentModel.DataAnnotations;

namespace IMAS_API_Example.Shared.Domain
{
    public class BaseClass
    {
        //[Key]
        //public Guid ID { get; set; }
        public bool ISDELETED { get; set; }
        public string? SYSUSERCREATED { get; set; }
        public DateTime? SYSDATECREATED { get; set; }
        public string? SYSUSERMODIFIED { get; set; }
        public DateTime? SYSDATEMODIFIED { get; set; }

        public BaseClass()
        {
            //ID = Guid.NewGuid();
            ISDELETED = false;
        }

        public void SetCreated(string user)
        {
            SYSUSERCREATED = user;
            SYSDATECREATED = DateTime.Now;
        }

        public void SetModified(string user)
        {
            SYSUSERMODIFIED = user;
            SYSDATEMODIFIED = DateTime.Now;
        }

        public void SetDeleted()
        {
            ISDELETED = true;
        }
    }
}
