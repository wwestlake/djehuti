#!/usr/bin/env python3
"""
S3 Batch Upload Tool
Upload files/directories to S3 bucket with organized folder structure.

Usage:
    python3 s3-batch-upload.py --source /path/to/files --dest archives/logs/

AWS Credentials:
    Set via environment variables (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY)
    or configured in ~/.aws/credentials
"""

import boto3
import argparse
import os
from pathlib import Path
from datetime import datetime

# Configuration
BUCKET = "us-east-1-886110331954-us-east-2-an"
REGION = "us-east-2"

class S3BatchUploader:
    def __init__(self, bucket, region, access_key=None, secret_key=None):
        """Initialize S3 uploader with credentials"""
        kwargs = {'region_name': region}
        if access_key and secret_key:
            kwargs['aws_access_key_id'] = access_key
            kwargs['aws_secret_access_key'] = secret_key

        self.s3 = boto3.client('s3', **kwargs)
        self.bucket = bucket
        self.uploaded = 0
        self.failed = 0
        self.total_size = 0

    def upload_file(self, local_path, s3_path):
        """Upload a single file to S3"""
        try:
            file_size = os.path.getsize(local_path)
            self.s3.upload_file(local_path, self.bucket, s3_path)
            self.uploaded += 1
            self.total_size += file_size
            print(f"  ✓ {s3_path:60} {file_size/1024/1024:>8.2f} MB")
            return True
        except Exception as e:
            self.failed += 1
            print(f"  ✗ {s3_path:60} ERROR: {str(e)}")
            return False

    def upload_directory(self, local_dir, s3_prefix):
        """Upload a directory and all files to S3"""
        local_path = Path(local_dir)

        if not local_path.exists():
            print(f"ERROR: Directory not found: {local_dir}")
            return False

        if not local_path.is_dir():
            print(f"ERROR: Not a directory: {local_dir}")
            return False

        print(f"Uploading from: {local_dir}")
        print(f"Destination:    s3://{self.bucket}/{s3_prefix}")
        print(f"Timestamp:      {datetime.now().isoformat()}")
        print()

        # Count files first
        all_files = list(local_path.rglob('*'))
        file_count = len([f for f in all_files if f.is_file()])
        print(f"Files to upload: {file_count}")
        print()

        # Upload files
        for local_file in local_path.rglob('*'):
            if local_file.is_file():
                # Calculate relative path
                rel_path = local_file.relative_to(local_path)
                s3_key = f"{s3_prefix}{rel_path}".replace("\\", "/")
                self.upload_file(str(local_file), s3_key)

        # Summary
        print()
        print("=" * 80)
        print(f"Upload complete!")
        print(f"  Successful:    {self.uploaded} files")
        print(f"  Failed:        {self.failed} files")
        print(f"  Total size:    {self.total_size/1024/1024:.2f} MB")
        print(f"  S3 location:   s3://{self.bucket}/{s3_prefix}")
        print("=" * 80)

        return self.failed == 0


def main():
    parser = argparse.ArgumentParser(
        description='Upload files to S3 with organized folder structure'
    )
    parser.add_argument('--source', required=True, help='Local directory to upload')
    parser.add_argument('--dest', required=True, help='S3 destination path (e.g., archives/logs/2024/07/)')
    parser.add_argument('--key', help='AWS Access Key ID (if not in environment)')
    parser.add_argument('--secret', help='AWS Secret Access Key (if not in environment)')
    parser.add_argument('--bucket', default=BUCKET, help='S3 bucket name')
    parser.add_argument('--region', default=REGION, help='AWS region')

    args = parser.parse_args()

    # Ensure destination ends with /
    dest = args.dest if args.dest.endswith('/') else args.dest + '/'

    uploader = S3BatchUploader(args.bucket, args.region, args.key, args.secret)
    success = uploader.upload_directory(args.source, dest)

    return 0 if success else 1


if __name__ == '__main__':
    exit(main())
