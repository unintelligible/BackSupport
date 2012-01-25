using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace BackSupportTests.TestObjects
{
    public class User
    {
        [Required]
        public string Name { get; set; }
        [Required]
        [Range(0, 100)]
        public int Age { get; set; }
        [RegularExpression("Private|Public")]
        public string CustomerType { get; set; }
        [StringLength(100, MinimumLength = 5)]
        [Display(Description = "Full Name")]
        public string FullName { get; set; }
        public string OptionalField { get; set; }
        public DateTime DateOfBirth { get; set; }
        public Group Group { get; set; }
    }

    public class Group
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; }
    }
}
