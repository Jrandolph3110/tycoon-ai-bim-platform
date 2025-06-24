/**
 * Time Manager - Simplified TypeScript version for Tycoon
 * Handles time-related operations and temporal awareness
 */

export interface TimeInfo {
    timestamp: string;
    iso: string;
    unix: number;
    timezone: string;
    formatted: string;
    relative: string;
}

export interface TimeRange {
    start: Date;
    end: Date;
}

export class TimeManager {
    private preferredTimezone: string = 'local';

    constructor() {
        // Initialize with local timezone
    }

    /**
     * Get current date and time information
     */
    getCurrentDateTime(format: 'detailed' | 'simple' | 'iso' = 'detailed', timezone?: string): TimeInfo {
        const now = new Date();
        const tz = timezone || this.preferredTimezone;
        
        return {
            timestamp: now.toISOString(),
            iso: now.toISOString(),
            unix: now.getTime(),
            timezone: tz,
            formatted: this.formatDateTime(now, format),
            relative: this.getRelativeTime(now)
        };
    }

    /**
     * Set preferred timezone
     */
    setPreferredTimezone(timezone: string): void {
        this.preferredTimezone = timezone;
    }

    /**
     * Calculate time elapsed since a timestamp
     */
    getTimeSince(timestamp: string | number): {
        elapsed: number;
        formatted: string;
        relative: string;
    } {
        const past = typeof timestamp === 'string' ? new Date(timestamp) : new Date(timestamp);
        const now = new Date();
        const elapsed = now.getTime() - past.getTime();

        return {
            elapsed,
            formatted: this.formatDuration(elapsed),
            relative: this.getRelativeTime(past)
        };
    }

    /**
     * Format date and time
     */
    private formatDateTime(date: Date, format: 'detailed' | 'simple' | 'iso'): string {
        switch (format) {
            case 'iso':
                return date.toISOString();
            case 'simple':
                return date.toLocaleString();
            case 'detailed':
            default:
                return `${date.toLocaleDateString()} ${date.toLocaleTimeString()}`;
        }
    }

    /**
     * Get relative time description
     */
    private getRelativeTime(date: Date): string {
        const now = new Date();
        const diff = now.getTime() - date.getTime();
        const seconds = Math.floor(diff / 1000);
        const minutes = Math.floor(seconds / 60);
        const hours = Math.floor(minutes / 60);
        const days = Math.floor(hours / 24);

        if (seconds < 60) return 'just now';
        if (minutes < 60) return `${minutes} minute${minutes !== 1 ? 's' : ''} ago`;
        if (hours < 24) return `${hours} hour${hours !== 1 ? 's' : ''} ago`;
        if (days < 30) return `${days} day${days !== 1 ? 's' : ''} ago`;
        
        return date.toLocaleDateString();
    }

    /**
     * Format duration in milliseconds
     */
    private formatDuration(ms: number): string {
        const seconds = Math.floor(ms / 1000);
        const minutes = Math.floor(seconds / 60);
        const hours = Math.floor(minutes / 60);
        const days = Math.floor(hours / 24);

        if (days > 0) return `${days}d ${hours % 24}h ${minutes % 60}m`;
        if (hours > 0) return `${hours}h ${minutes % 60}m`;
        if (minutes > 0) return `${minutes}m ${seconds % 60}s`;
        return `${seconds}s`;
    }

    /**
     * Create time range
     */
    createTimeRange(start: string | Date, end: string | Date): TimeRange {
        return {
            start: typeof start === 'string' ? new Date(start) : start,
            end: typeof end === 'string' ? new Date(end) : end
        };
    }

    /**
     * Check if date is within range
     */
    isWithinRange(date: Date, range: TimeRange): boolean {
        return date >= range.start && date <= range.end;
    }
}
