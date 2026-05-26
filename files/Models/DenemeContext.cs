using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace project.Models;

public partial class DenemeContext : DbContext
{
    public DenemeContext()
    {
    }

    public DenemeContext(DbContextOptions<DenemeContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Customization> Customizations { get; set; }

    public virtual DbSet<MenuItem> MenuItems { get; set; }
    
    public DbSet<Category> Categories { get; set; }
    
    public virtual DbSet<ChatMessage> ChatMessages { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<OrderItemOption> OrderItemOptions { get; set; }

    public virtual DbSet<Rating> Ratings { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SystemLog> SystemLogs { get; set; }

    public virtual DbSet<User> Users { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customization>(entity =>
        {
            entity.HasKey(e => e.OptionId).HasName("Customizations_pk");

            entity.Property(e => e.OptionId).HasColumnName("OptionID");
            entity.Property(e => e.ItemId).HasColumnName("ItemID");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.OptionGroup).HasMaxLength(50);
            entity.Property(e => e.PriceChange)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(15, 2)");

            entity.HasOne(d => d.Item).WithMany(p => p.Customizations)
                .HasForeignKey(d => d.ItemId)
                .HasConstraintName("Customizations_MenuItems_ItemID_fk");
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(e => e.ItemId).HasName("MenuItems_pk");

            entity.Property(e => e.ItemId).HasColumnName("ItemID");
            entity.Property(e => e.CatererId).HasColumnName("CatererID");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Price).HasColumnType("decimal(15, 2)");
            entity.Property(e => e.Title).HasMaxLength(100);

            entity.HasOne(d => d.Caterer).WithMany(p => p.MenuItems)
                .HasForeignKey(d => d.CatererId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MenuItems_Users_UserID_fk");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("Orders_pk");

            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.CatererId).HasColumnName("CatererID");
            entity.Property(e => e.OrderDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(15, 2)");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Caterer).WithMany(p => p.OrderCaterers)
                .HasForeignKey(d => d.CatererId)
                .HasConstraintName("Orders_Users_UserID_fk_2");

            entity.HasOne(d => d.User).WithMany(p => p.OrderUsers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("Orders_Users_UserID_fk");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.OrderItemId).HasName("OrderItems_pk");

            entity.Property(e => e.OrderItemId).HasColumnName("OrderItemID");
            entity.Property(e => e.ItemId).HasColumnName("ItemID");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(15, 2)");

            entity.HasOne(d => d.Item).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ItemId)
                .HasConstraintName("OrderItems_MenuItems_ItemID_fk");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("OrderItems_Orders_OrderID_fk");
        });

        modelBuilder.Entity<OrderItemOption>(entity =>
        {
            entity.HasKey(e => e.RecordId).HasName("OrderItemOptions_pk");

            entity.Property(e => e.RecordId).HasColumnName("RecordID");
            entity.Property(e => e.OptionId).HasColumnName("OptionID");
            entity.Property(e => e.OrderItemId).HasColumnName("OrderItemID");

            entity.HasOne(d => d.Option).WithMany(p => p.OrderItemOptions)
                .HasForeignKey(d => d.OptionId)
                .HasConstraintName("OrderItemOptions_Customizations_OptionID_fk");

            entity.HasOne(d => d.OrderItem).WithMany(p => p.OrderItemOptions)
                .HasForeignKey(d => d.OrderItemId)
                .HasConstraintName("OrderItemOptions_OrderItems_OrderItemID_fk");
        });

        modelBuilder.Entity<Rating>(entity =>
        {
            entity.HasKey(e => e.RatingId).HasName("Ratings_pk");

            entity.Property(e => e.RatingId).HasColumnName("RatingID");
            entity.Property(e => e.Comment).HasMaxLength(500);
            entity.Property(e => e.OrderId).HasColumnName("OrderID");

            entity.HasOne(d => d.Order).WithMany(p => p.Ratings)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("Ratings_Orders_OrderID_fk");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("Roles_pk");

            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<SystemLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("SystemLogs_pk");

            entity.Property(e => e.LogId).HasColumnName("LogID");
            entity.Property(e => e.ActionType).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Timestamp)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithMany(p => p.SystemLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("SystemLogs_Users_UserID_fk");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("Users_pk");

            entity.HasIndex(e => e.Email, "Email").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasColumnName("Full Name");
            entity.Property(e => e.Is2Faenabled)
                .HasDefaultValue(false)
                .HasColumnName("Is2FAEnabled");
            entity.Property(e => e.Latitude).HasColumnType("decimal(10, 8)");
            entity.Property(e => e.Longitude).HasColumnType("decimal(11, 8)");
            entity.Property(e => e.RoleId).HasColumnName("RoleID");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("Users_Roles_RoleID_fk");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
