-- SQL Injection Challenge Database
-- This file contains the database schema and sample data

CREATE TABLE users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    username TEXT UNIQUE NOT NULL,
    password TEXT NOT NULL,
    email TEXT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Sample users (password is 'admin123' for admin user)
INSERT INTO users (username, password, email) VALUES
('admin', 'admin123', 'admin@vulnarena.local'),
('user1', 'password123', 'user1@example.com'),
('user2', 'password456', 'user2@example.com'),
('guest', 'guest123', 'guest@example.com');

-- Create index on username for faster lookups
CREATE INDEX idx_users_username ON users(username); 