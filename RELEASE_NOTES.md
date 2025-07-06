# Release Notes

All notable changes to this project will be documented in this file.

## [Unreleased]
### Added
- Real-time processing updates via SignalR
- Transaction categorization rules engine
- Enhanced data export (CSV, Excel, QIF, OFX, JSON)
- Email/SMS notifications
- Scheduled job management
- Performance monitoring and metrics

### Fixed
- Scheduler now validates parameters and handles errors gracefully
- Rules engine now applies rules only to the correct user's transactions
- ExportService handles empty collections safely
- NotificationService uses UTC timestamps
- Timer callback exceptions are now handled

---

## [1.0.0] - 2024-07-01
### Added
- Initial release with PDF parsing, transaction import, and web dashboard 