#!/usr/bin/env swift
import CoreGraphics
import Foundation

guard CommandLine.arguments.count == 2,
      let requestedPid = Int32(CommandLine.arguments[1]) else {
    fputs("usage: macos-window-id.swift <pid>\n", stderr)
    exit(2)
}

// Direct-window capture can render a layer-zero window even when macOS has placed
// that process on another Space. Restricting discovery to the current Space makes
// an otherwise valid isolated capture wait until the full-plan timeout whenever a
// normal installed Libation window is already active.
let options: CGWindowListOption = [.excludeDesktopElements]
guard let windows = CGWindowListCopyWindowInfo(options, kCGNullWindowID)
    as? [[CFString: Any]] else {
    exit(1)
}

let matches = windows.compactMap { window -> (id: CGWindowID, bounds: CGRect)? in
    guard let owner = window[kCGWindowOwnerPID] as? NSNumber,
          owner.int32Value == requestedPid,
          let layer = window[kCGWindowLayer] as? NSNumber,
          layer.intValue == 0,
          let number = window[kCGWindowNumber] as? NSNumber,
          let boundsDictionary = window[kCGWindowBounds] as? NSDictionary,
          let bounds = CGRect(dictionaryRepresentation: boundsDictionary),
          bounds.width > 0,
          bounds.height > 0 else {
        return nil
    }
    return (number.uint32Value, bounds)
}

guard let window = matches.max(by: {
    $0.bounds.width * $0.bounds.height < $1.bounds.width * $1.bounds.height
}) else {
    exit(1)
}

print("\(window.id)\t\(Int(window.bounds.width.rounded()))\t\(Int(window.bounds.height.rounded()))")
