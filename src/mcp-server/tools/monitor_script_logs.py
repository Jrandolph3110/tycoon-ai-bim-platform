"""
Tycoon Script Log Monitor - MCP Tool
Provides real-time monitoring of Tycoon script execution logs for AI assistant analysis
"""

import os
import time
import json
from pathlib import Path
from typing import Dict, List, Optional, Union
from datetime import datetime, timedelta


def monitor_script_logs(
    log_type: str = "all",
    tail_lines: int = 50,
    follow: bool = False,
    filter_level: str = "all",
    since_minutes: int = 0
) -> Dict[str, Union[str, List[Dict]]]:
    """
    Monitor Tycoon script execution logs for development and debugging
    
    Args:
        log_type: Type of logs to monitor ("all", "renumber", "tycoon", "console")
        tail_lines: Number of recent lines to return (default: 50)
        follow: Whether to monitor for new entries (default: False)
        filter_level: Log level filter ("all", "error", "success", "info", "warning")
        since_minutes: Only show logs from last N minutes (0 = all)
    
    Returns:
        Dictionary containing log entries and metadata
    """
    
    try:
        # Define log file paths
        appdata_path = os.path.expandvars("%APPDATA%")
        tycoon_dir = os.path.join(appdata_path, "Tycoon")
        workspace_dir = r"C:\RevitAI"
        
        log_files = {}
        
        if log_type in ["all", "renumber"]:
            renumber_log = os.path.join(tycoon_dir, "ReNumber_Debug.log")
            if os.path.exists(renumber_log):
                log_files["renumber"] = renumber_log
        
        if log_type in ["all", "tycoon"]:
            # Find the most recent Tycoon log file
            tycoon_logs = []
            for file in os.listdir(workspace_dir):
                if file.startswith("Tycoon_") and file.endswith(".log"):
                    full_path = os.path.join(workspace_dir, file)
                    tycoon_logs.append((full_path, os.path.getmtime(full_path)))
            
            if tycoon_logs:
                # Get the most recent Tycoon log
                latest_tycoon_log = max(tycoon_logs, key=lambda x: x[1])[0]
                log_files["tycoon"] = latest_tycoon_log
        
        if not log_files:
            return {
                "status": "no_logs_found",
                "message": "No log files found. Make sure Tycoon is running and scripts have been executed.",
                "searched_paths": [tycoon_dir, workspace_dir],
                "entries": []
            }
        
        # Collect log entries
        all_entries = []
        cutoff_time = None
        
        if since_minutes > 0:
            cutoff_time = datetime.now() - timedelta(minutes=since_minutes)
        
        for log_name, log_path in log_files.items():
            entries = _parse_log_file(log_path, log_name, tail_lines, filter_level, cutoff_time)
            all_entries.extend(entries)
        
        # Sort by timestamp
        all_entries.sort(key=lambda x: x.get("timestamp", ""))
        
        # Apply tail limit to combined results
        if len(all_entries) > tail_lines:
            all_entries = all_entries[-tail_lines:]
        
        return {
            "status": "success",
            "log_files_monitored": list(log_files.keys()),
            "total_entries": len(all_entries),
            "filter_applied": filter_level,
            "time_range": f"Last {since_minutes} minutes" if since_minutes > 0 else "All available",
            "entries": all_entries,
            "monitoring_info": {
                "renumber_log_exists": "renumber" in log_files,
                "tycoon_log_exists": "tycoon" in log_files,
                "last_updated": datetime.now().isoformat()
            }
        }
        
    except Exception as e:
        return {
            "status": "error",
            "message": f"Error monitoring logs: {str(e)}",
            "entries": []
        }


def _parse_log_file(
    file_path: str, 
    log_source: str, 
    tail_lines: int, 
    filter_level: str,
    cutoff_time: Optional[datetime]
) -> List[Dict]:
    """Parse a log file and extract structured entries"""
    
    entries = []
    
    try:
        with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
            lines = f.readlines()
        
        # Get the last N lines
        if len(lines) > tail_lines:
            lines = lines[-tail_lines:]
        
        for line_num, line in enumerate(lines, 1):
            line = line.strip()
            if not line:
                continue
            
            entry = _parse_log_line(line, log_source, line_num)
            
            # Apply time filter
            if cutoff_time and entry.get("parsed_timestamp"):
                try:
                    entry_time = datetime.fromisoformat(entry["parsed_timestamp"])
                    if entry_time < cutoff_time:
                        continue
                except:
                    pass
            
            # Apply level filter
            if filter_level != "all" and entry.get("level", "").lower() != filter_level.lower():
                continue
            
            entries.append(entry)
    
    except Exception as e:
        entries.append({
            "source": log_source,
            "level": "error",
            "message": f"Error reading log file {file_path}: {str(e)}",
            "timestamp": datetime.now().isoformat(),
            "raw_line": ""
        })
    
    return entries


def _parse_log_line(line: str, source: str, line_num: int) -> Dict:
    """Parse a single log line into structured data"""
    
    entry = {
        "source": source,
        "line_number": line_num,
        "raw_line": line,
        "timestamp": "",
        "parsed_timestamp": "",
        "level": "info",
        "message": line,
        "emojis": [],
        "contains_error": False,
        "contains_success": False
    }
    
    # Extract timestamp if present
    timestamp_patterns = [
        r'\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})\]',  # [2025-07-13 21:45:32.123]
        r'\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\]',         # [2025-07-13 21:45:32]
        r'\[(\d{2}:\d{2}:\d{2}\.\d{3})\]',                     # [21:45:32.123]
        r'\[(\d{2}:\d{2}:\d{2})\]'                             # [21:45:32]
    ]
    
    import re
    for pattern in timestamp_patterns:
        match = re.search(pattern, line)
        if match:
            entry["timestamp"] = match.group(1)
            try:
                # Try to parse as full datetime
                if len(match.group(1)) > 10:  # Has date
                    parsed_time = datetime.strptime(match.group(1), "%Y-%m-%d %H:%M:%S.%f" if "." in match.group(1) else "%Y-%m-%d %H:%M:%S")
                else:  # Time only, use today's date
                    time_part = match.group(1)
                    today = datetime.now().date()
                    parsed_time = datetime.strptime(f"{today} {time_part}", "%Y-%m-%d %H:%M:%S.%f" if "." in time_part else "%Y-%m-%d %H:%M:%S")
                
                entry["parsed_timestamp"] = parsed_time.isoformat()
            except:
                pass
            break
    
    # Determine log level from content
    line_lower = line.lower()
    if any(indicator in line for indicator in ["❌", "ERROR", "Failed", "Exception"]):
        entry["level"] = "error"
        entry["contains_error"] = True
    elif any(indicator in line for indicator in ["⚠️", "WARN", "Warning"]):
        entry["level"] = "warning"
    elif any(indicator in line for indicator in ["✅", "SUCCESS", "completed", "🔥", "💾"]):
        entry["level"] = "success"
        entry["contains_success"] = True
    elif any(indicator in line for indicator in ["🔍", "🔄", "📊", "INFO"]):
        entry["level"] = "info"
    
    # Extract emojis
    emoji_pattern = r'[\U0001F600-\U0001F64F\U0001F300-\U0001F5FF\U0001F680-\U0001F6FF\U0001F1E0-\U0001F1FF\U00002600-\U000027BF]'
    emojis = re.findall(emoji_pattern, line)
    entry["emojis"] = emojis
    
    # Clean message (remove timestamp and common prefixes)
    message = line
    for pattern in timestamp_patterns:
        message = re.sub(pattern, '', message).strip()
    
    # Remove common log prefixes
    prefixes_to_remove = [
        r'^\[INFO\]\s*\[Tycoon\]\s*',
        r'^\[ERROR\]\s*\[Tycoon\]\s*',
        r'^\[WARN\]\s*\[Tycoon\]\s*'
    ]
    
    for prefix in prefixes_to_remove:
        message = re.sub(prefix, '', message, flags=re.IGNORECASE).strip()
    
    entry["message"] = message
    
    return entry


# MCP Tool Registration
TOOL_DEFINITION = {
    "name": "monitor_script_logs",
    "description": "Monitor Tycoon script execution logs for real-time development feedback and debugging",
    "inputSchema": {
        "type": "object",
        "properties": {
            "log_type": {
                "type": "string",
                "enum": ["all", "renumber", "tycoon", "console"],
                "default": "all",
                "description": "Type of logs to monitor"
            },
            "tail_lines": {
                "type": "integer",
                "default": 50,
                "minimum": 1,
                "maximum": 500,
                "description": "Number of recent lines to return"
            },
            "follow": {
                "type": "boolean",
                "default": False,
                "description": "Whether to monitor for new entries"
            },
            "filter_level": {
                "type": "string",
                "enum": ["all", "error", "success", "info", "warning"],
                "default": "all",
                "description": "Log level filter"
            },
            "since_minutes": {
                "type": "integer",
                "default": 0,
                "minimum": 0,
                "description": "Only show logs from last N minutes (0 = all)"
            }
        }
    }
}
