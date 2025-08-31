#!/bin/bash
pkill -9 -f 'Server' || true
sudo rm -rf /home/ec2-user/CG_Server || true