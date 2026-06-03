using System;
using System.Collections.Generic;
using OnPremAppSample.Models;

namespace OnPremAppSample.Data;

public static class MockDataStore
{
    public static List<Course> Courses { get; } = new()
    {
        new Course
        {
            Id = 1,
            Title = "Introduction to Cloud Computing",
            Description = "Learn the core concepts of Cloud Computing, virtualization, and key cloud models like IaaS, PaaS, and SaaS using AWS, Azure, and Google Cloud.",
            Category = "Cloud Technology",
            Difficulty = "Beginner",
            Duration = "6 Weeks (18 Hours)",
            Instructor = "Dr. Alice Vance",
            ImagePath = "/uploads/sample-course-image.txt",
            Price = 49.99m
        },
        new Course
        {
            Id = 2,
            Title = "Mastering ASP.NET Core MVC & .NET 9",
            Description = "Build premium, secure, and scalable web applications using ASP.NET Core MVC, Bootstrap 5, dependency injection, and modern C# 13 features.",
            Category = "Web Development",
            Difficulty = "Intermediate",
            Duration = "8 Weeks (24 Hours)",
            Instructor = "Prof. Robert Miller",
            ImagePath = "/uploads/sample-course-image.txt",
            Price = 79.99m
        },
        new Course
        {
            Id = 3,
            Title = "Terraform Infrastructure as Code (IaC)",
            Description = "Automate your infrastructure deployments. Write, plan, and apply Terraform configurations for local development via LocalStack and AWS environments.",
            Category = "DevOps",
            Difficulty = "Advanced",
            Duration = "4 Weeks (12 Hours)",
            Instructor = "Ing. Sarah Croft",
            ImagePath = "/uploads/sample-course-image.txt",
            Price = 99.50m
        },
        new Course
        {
            Id = 4,
            Title = "LocalStack: Offline Cloud Simulation",
            Description = "Simulate cloud-native services (S3, SQS, Lambda, DynamoDB) right on your local machine. Speed up dev cycles and lower testing costs.",
            Category = "DevOps",
            Difficulty = "Intermediate",
            Duration = "3 Weeks (9 Hours)",
            Instructor = "Ing. Sarah Croft",
            ImagePath = "/uploads/sample-course-image.txt",
            Price = 29.99m
        }
    };

    public static List<Student> Students { get; } = new()
    {
        new Student
        {
            Id = 101,
            FullName = "Nguyen Van A",
            Email = "vana.nguyen@educourse.edu.vn",
            EnrollmentDate = DateTime.Now.AddMonths(-3),
            Status = "Active",
            Major = "Computer Science"
        },
        new Student
        {
            Id = 102,
            FullName = "Tran Thi B",
            Email = "thib.tran@educourse.edu.vn",
            EnrollmentDate = DateTime.Now.AddMonths(-2),
            Status = "Active",
            Major = "Software Engineering"
        },
        new Student
        {
            Id = 103,
            FullName = "Le Hoang C",
            Email = "hoangc.le@educourse.edu.vn",
            EnrollmentDate = DateTime.Now.AddMonths(-5),
            Status = "Completed",
            Major = "Information Systems"
        },
        new Student
        {
            Id = 104,
            FullName = "Pham Minh D",
            Email = "minhd.pham@educourse.edu.vn",
            EnrollmentDate = DateTime.Now.AddDays(-15),
            Status = "Active",
            Major = "Cyber Security"
        },
        new Student
        {
            Id = 105,
            FullName = "Hoang Xuan E",
            Email = "xuane.hoang@educourse.edu.vn",
            EnrollmentDate = DateTime.Now.AddMonths(-1),
            Status = "Suspended",
            Major = "Data Science"
        }
    };

    public static List<Lesson> Lessons { get; } = new()
    {
        // Lessons for Course 1: Introduction to Cloud Computing
        new Lesson
        {
            Id = 201,
            CourseId = 1,
            CourseTitle = "Introduction to Cloud Computing",
            Title = "Understanding Virtualization and Hypervisors",
            Description = "Explore how physical hardware is abstracted into virtual machines using Type-1 and Type-2 hypervisors.",
            DurationMinutes = 45,
            DocumentPath = "/uploads/sample-lesson-document.txt"
        },
        new Lesson
        {
            Id = 202,
            CourseId = 1,
            CourseTitle = "Introduction to Cloud Computing",
            Title = "Cloud Service Models: IaaS, PaaS, and SaaS",
            Description = "Learn the differences in management responsibilities across Infrastructure, Platform, and Software service models.",
            DurationMinutes = 35,
            DocumentPath = "/uploads/sample-lesson-document.txt"
        },
        // Lessons for Course 2: Mastering ASP.NET Core MVC
        new Lesson
        {
            Id = 203,
            CourseId = 2,
            CourseTitle = "Mastering ASP.NET Core MVC & .NET 9",
            Title = "Introduction to the MVC Pattern in ASP.NET Core",
            Description = "Deep dive into Controllers, Action Results, Views, ViewModels, and the request lifecycle.",
            DurationMinutes = 60,
            DocumentPath = "/uploads/sample-lesson-document.txt"
        },
        new Lesson
        {
            Id = 204,
            CourseId = 2,
            CourseTitle = "Mastering ASP.NET Core MVC & .NET 9",
            Title = "Dependency Injection and Configuration Lifecycles",
            Description = "Configure and resolve transient, scoped, and singleton services, and read appsettings.json dynamically.",
            DurationMinutes = 50,
            DocumentPath = "/uploads/sample-lesson-document.txt"
        },
        // Lessons for Course 3: Terraform IaC
        new Lesson
        {
            Id = 205,
            CourseId = 3,
            CourseTitle = "Terraform Infrastructure as Code (IaC)",
            Title = "Declaring Providers and State File Fundamentals",
            Description = "Understand the provider registry, basic resource syntax, and the importance of the terraform.tfstate file.",
            DurationMinutes = 40,
            DocumentPath = "/uploads/sample-lesson-document.txt"
        },
        // Lessons for Course 4: LocalStack
        new Lesson
        {
            Id = 206,
            CourseId = 4,
            CourseTitle = "LocalStack: Offline Cloud Simulation",
            Title = "Setting up LocalStack using Docker Compose",
            Description = "Learn how to configure LocalStack environment variables, expose service ports, and verify status via local endpoints.",
            DurationMinutes = 30,
            DocumentPath = "/uploads/sample-lesson-document.txt"
        }
    };
}
